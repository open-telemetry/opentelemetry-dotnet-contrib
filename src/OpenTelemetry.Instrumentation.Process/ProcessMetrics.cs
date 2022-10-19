// <copyright file="ProcessMetrics.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal sealed class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    private readonly Diagnostics.Process currentProcess = Diagnostics.Process.GetCurrentProcess();

    // vars for calculating CPU utilization
    private DateTime lastCollectionTimeUtc;
    private double lastCollectedUserProcessorTime;
    private double lastCollectedPrivilegedProcessorTime;

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
        this.lastCollectionTimeUtc = DateTime.UtcNow;
        this.lastCollectedUserProcessorTime = this.currentProcess.UserProcessorTime.TotalSeconds;
        this.lastCollectedPrivilegedProcessorTime = this.currentProcess.PrivilegedProcessorTime.TotalSeconds;

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () =>
            {
                this.currentProcess.Refresh();
                return this.currentProcess.WorkingSet64;
            },
            unit: "By",
            description: "The amount of physical memory allocated for this process.");

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () =>
            {
                this.currentProcess.Refresh();
                return this.currentProcess.PrivateMemorySize64;
            },
            unit: "By",
            description: "The amount of virtual memory allocated for this process that cannot be shared with other processes.");

        this.MeterInstance.CreateObservableCounter(
            "process.cpu.time",
            () =>
            {
                this.currentProcess.Refresh();
                return new[]
                {
                    new Measurement<double>(this.currentProcess.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "user")),
                    new Measurement<double>(this.currentProcess.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "system")),
                };
            },
            unit: "s",
            description: "Total CPU seconds broken down by different states.");

        this.MeterInstance.CreateObservableGauge(
            "process.cpu.utilization",
            () =>
            {
                this.currentProcess.Refresh();
                return this.GetCpuUtilization();
            },
            unit: "1",
            description: "Difference in process.cpu.time since the last measurement, divided by the elapsed time and number of CPUs available to the process.");

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.threads",
            () =>
            {
                this.currentProcess.Refresh();
                return this.currentProcess.Threads.Count;
            },
            unit: "{threads}",
            description: "Process threads count.");
    }

    private IEnumerable<Measurement<double>> GetCpuUtilization()
    {
        var elapsedTimeForAllCpus = (DateTime.UtcNow - this.lastCollectionTimeUtc).TotalSeconds * Environment.ProcessorCount;
        var userProcessorUtilization = (this.currentProcess.UserProcessorTime.TotalSeconds - this.lastCollectedUserProcessorTime) / elapsedTimeForAllCpus;
        var privilegedProcessorUtilization = (this.currentProcess.PrivilegedProcessorTime.TotalSeconds - this.lastCollectedPrivilegedProcessorTime) / elapsedTimeForAllCpus;

        this.lastCollectionTimeUtc = DateTime.UtcNow;
        this.lastCollectedUserProcessorTime = this.currentProcess.UserProcessorTime.TotalSeconds;
        this.lastCollectedPrivilegedProcessorTime = this.currentProcess.PrivilegedProcessorTime.TotalSeconds;

        return new[]
        {
            new Measurement<double>(Math.Min(userProcessorUtilization, 1D), new KeyValuePair<string, object?>("state", "user")),
            new Measurement<double>(Math.Min(privilegedProcessorUtilization, 1D), new KeyValuePair<string, object?>("state", "system")),
        };
    }
}
