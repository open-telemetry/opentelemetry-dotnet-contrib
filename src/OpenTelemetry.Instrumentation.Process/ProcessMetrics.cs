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

    static ProcessMetrics()
    {
        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.usage",
            () =>
            {
                using var process = Diagnostics.Process.GetCurrentProcess();
                return process.WorkingSet64;
            },
            unit: "By",
            description: "The amount of physical memory in use.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.virtual",
            () =>
            {
                using var process = Diagnostics.Process.GetCurrentProcess();
                return process.VirtualMemorySize64;
            },
            unit: "By",
            description: "The amount of committed virtual memory.");

        MeterInstance.CreateObservableCounter(
            "process.cpu.time",
            () =>
            {
                using var process = Diagnostics.Process.GetCurrentProcess();
                return new[]
                {
                    new Measurement<double>(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("cpu.mode", "user")),
                    new Measurement<double>(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("cpu.mode", "system")),
                };
            },
            unit: "s",
            description: "Total CPU seconds broken down by different CPU modes.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.thread.count",
            () =>
            {
                using var process = Diagnostics.Process.GetCurrentProcess();
                return process.Threads.Count;
            },
            unit: "{thread}",
            description: "Process threads count.");

        MeterInstance.CreateObservableGauge(
            "process.uptime",
            () =>
            {
                using var process = Diagnostics.Process.GetCurrentProcess();
                return (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds;
            },
            unit: "s",
            description: "The time the process has been running.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MeterInstance.CreateObservableUpDownCounter(
                "process.windows.handle.count",
                () =>
                {
                    using var process = Diagnostics.Process.GetCurrentProcess();
                    return process.HandleCount;
                },
                unit: "{handle}",
                description: "Number of handles held by the process.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            MeterInstance.CreateObservableUpDownCounter(
                "process.unix.file_descriptor.count",
                () =>
                {
                    using var process = Diagnostics.Process.GetCurrentProcess();
                    return process.HandleCount;
                },
                unit: "{file_descriptor}",
                description: "Number of unix file descriptors in use by the process.");
        }
    }
}
