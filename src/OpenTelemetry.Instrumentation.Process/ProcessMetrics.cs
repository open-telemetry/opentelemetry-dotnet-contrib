// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal sealed class ProcessMetrics
{
    internal static readonly Version SemanticConventionsVersion = new(1, 43, 0);
    internal static readonly Meter MeterInstance = Metrics.MeterFactory.Create<ProcessMetrics>(SemanticConventionsVersion);

    private static readonly long SnapshotTtlTicks = Diagnostics.Stopwatch.Frequency / 100; // 10ms

    private static readonly Diagnostics.Process CurrentProcess = Diagnostics.Process.GetCurrentProcess();
    private static readonly DateTime ProcessStartTimeUtc = CurrentProcess.StartTime.ToUniversalTime();
    private static readonly Lock SnapshotLock = new();

    private static long snapshotTimestamp;

    static ProcessMetrics()
    {
        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.usage",
            static () => Measure(static (p) => p.WorkingSet64),
            unit: "By",
            description: "The amount of physical memory in use.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.virtual",
            static () => Measure(static (p) => p.VirtualMemorySize64),
            unit: "By",
            description: "The amount of committed virtual memory.");

        MeterInstance.CreateObservableCounter<double>(
            "process.cpu.time",
            static () =>
            {
                (var userSeconds, var privilegedSeconds) = Measure(
                    static (p) => (p.UserProcessorTime.TotalSeconds, p.PrivilegedProcessorTime.TotalSeconds));

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
            static () => Measure(static (p) => p.Threads.Count),
            unit: "{thread}",
            description: "Process threads count.");

        MeterInstance.CreateObservableGauge(
            "process.uptime",
            static () => (DateTime.UtcNow - ProcessStartTimeUtc).TotalSeconds,
            unit: "s",
            description: "The time the process has been running.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MeterInstance.CreateObservableUpDownCounter(
                "process.windows.handle.count",
                static () => Measure(static (p) => p.HandleCount),
                unit: "{handle}",
                description: "Number of handles held by the process.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            MeterInstance.CreateObservableUpDownCounter(
                "process.unix.file_descriptor.count",
                static () => Measure(static (p) => p.HandleCount),
                unit: "{file_descriptor}",
                description: "Number of unix file descriptors in use by the process.");
        }
    }

    private static T Measure<T>(Func<Diagnostics.Process, T> func)
    {
        lock (SnapshotLock)
        {
            RefreshSnapshotIfStale();
            return func(CurrentProcess);
        }
    }

    private static void RefreshSnapshotIfStale()
    {
        var now = Diagnostics.Stopwatch.GetTimestamp();

        if (now - snapshotTimestamp <= SnapshotTtlTicks)
        {
            return;
        }

        snapshotTimestamp = now;
        CurrentProcess.Refresh();
    }
}
