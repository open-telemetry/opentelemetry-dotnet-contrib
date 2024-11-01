// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Internal;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal sealed class ProcessMetrics
{
    internal static readonly Assembly Assembly = typeof(ProcessMetrics).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string MeterName = AssemblyName.Name;

    private static readonly Meter MeterInstance = new(MeterName, Assembly.GetPackageVersion());

    private static readonly Diagnostics.Process Process = Diagnostics.Process.GetCurrentProcess();

    static ProcessMetrics()
    {
        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.usage",
            () =>
            {
                return Process.WorkingSet64;
            },
            unit: "By",
            description: "The amount of physical memory in use.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.virtual",
            () =>
            {
                return Process.VirtualMemorySize64;
            },
            unit: "By",
            description: "The amount of committed virtual memory.");

        MeterInstance.CreateObservableCounter(
            "process.cpu.time",
            () =>
            {
                return new[]
                {
                    new Measurement<double>(Process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("process.cpu.state", "user")),
                    new Measurement<double>(Process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("process.cpu.state", "system")),
                };
            },
            unit: "s",
            description: "Total CPU seconds broken down by different states.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.cpu.count",
            () =>
            {
                return Environment.ProcessorCount;
            },
            unit: "{processors}",
            description: "The number of processors (CPU cores) available to the current process.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.thread.count",
            () =>
            {
                return Process.Threads.Count;
            },
            unit: "{thread}",
            description: "Process threads count.");
    }

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }
}
