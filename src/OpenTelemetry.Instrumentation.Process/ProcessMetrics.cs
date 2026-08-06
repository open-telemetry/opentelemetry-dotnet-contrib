// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal sealed class ProcessMetrics : IDisposable
{
    internal static readonly Version SemanticConventionsVersion = new(1, 43, 0);
    internal static readonly Meter MeterInstance = Metrics.MeterFactory.Create<ProcessMetrics>(SemanticConventionsVersion);

    private static readonly long SnapshotTtlTicks = Diagnostics.Stopwatch.Frequency / 100; // 10ms

    private readonly Diagnostics.Process currentProcess;
    private readonly DateTime processStartTimeUtc;
    private readonly Lock snapshotLock;

    private long snapshotTimestamp;

    public ProcessMetrics()
    {
        this.snapshotLock = new();
        this.currentProcess = Diagnostics.Process.GetCurrentProcess();
        this.processStartTimeUtc = this.currentProcess.StartTime.ToUniversalTime();

        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.usage",
            () => this.Measure((p) => p.WorkingSet64),
            unit: "By",
            description: "The amount of physical memory in use.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.virtual",
            () => this.Measure((p) => p.VirtualMemorySize64),
            unit: "By",
            description: "The amount of committed virtual memory.");

        MeterInstance.CreateObservableCounter<double>(
            "process.cpu.time",
            () =>
            {
                (var userSeconds, var privilegedSeconds) = this.Measure(
                    (p) => (p.UserProcessorTime.TotalSeconds, p.PrivilegedProcessorTime.TotalSeconds));

                return
                [
                    new Measurement<double>(userSeconds, new KeyValuePair<string, object?>("cpu.mode", "user")),
                    new Measurement<double>(privilegedSeconds, new KeyValuePair<string, object?>("cpu.mode", "system")),
                ];
            },
            unit: "s",
            description: "Total CPU seconds broken down by different CPU modes.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.thread.count",
            () => this.Measure((p) => p.Threads.Count),
            unit: "{thread}",
            description: "Process threads count.");

        MeterInstance.CreateObservableGauge(
            "process.uptime",
            () => (DateTime.UtcNow - this.processStartTimeUtc).TotalSeconds,
            unit: "s",
            description: "The time the process has been running.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MeterInstance.CreateObservableUpDownCounter(
                "process.windows.handle.count",
                () => this.Measure((p) => p.HandleCount),
                unit: "{handle}",
                description: "Number of handles held by the process.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            MeterInstance.CreateObservableUpDownCounter(
                "process.unix.file_descriptor.count",
                () => this.Measure((p) => p.HandleCount),
                unit: "{file_descriptor}",
                description: "Number of unix file descriptors in use by the process.");
        }
    }

    public void Dispose() => this.currentProcess.Dispose();

    private T Measure<T>(Func<Diagnostics.Process, T> func)
    {
        lock (this.snapshotLock)
        {
            this.RefreshSnapshotIfStale();
            return func(this.currentProcess);
        }
    }

    private void RefreshSnapshotIfStale()
    {
        var now = Diagnostics.Stopwatch.GetTimestamp();

        if (now - this.snapshotTimestamp <= SnapshotTtlTicks)
        {
            return;
        }

        this.snapshotTimestamp = now;
        this.currentProcess.Refresh();
    }
}
