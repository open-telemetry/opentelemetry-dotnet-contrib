// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal sealed class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly string MeterName = AssemblyName.Name;

    private static readonly Meter MeterInstance = new(MeterName, AssemblyName.Version.ToString());

    static ProcessMetrics()
    {
        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.usage",
            () =>
            {
                return Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            },
            unit: "By",
            description: "The amount of physical memory allocated for this process.");

        MeterInstance.CreateObservableUpDownCounter(
            "process.memory.virtual",
            () =>
            {
                return Diagnostics.Process.GetCurrentProcess().VirtualMemorySize64;
            },
            unit: "By",
            description: "The amount of committed virtual memory for this process.");

        MeterInstance.CreateObservableCounter(
            "process.cpu.time",
            () =>
            {
                var process = Diagnostics.Process.GetCurrentProcess();
                return new[]
                {
                    new Measurement<double>(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "user")),
                    new Measurement<double>(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "system")),
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
            "process.threads",
            () =>
            {
                return Diagnostics.Process.GetCurrentProcess().Threads.Count;
            },
            unit: "{threads}",
            description: "Process threads count.");
    }

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }
}
