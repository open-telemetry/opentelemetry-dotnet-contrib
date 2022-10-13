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
    private double? memoryUsage;
    private double? virtualMemoryUsage;
    private double? userProcessorTime;
    private double? privilegedProcessorTime;
    private int? numberOfThreads;
    private IEnumerable<Measurement<double>> cpuUtilization;

    // vars for calculating CPU utilization
    private DateTime lastCollectionTimeStamp;
    private double lastCollectionUserProcessorTime;
    private double lastCollectionPrivilegedProcessorTime;

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
        this.lastCollectionTimeStamp = this.currentProcess.StartTime;

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () =>
            {
                if (!this.memoryUsage.HasValue)
                {
                    this.Snapshot();
                }

                var value = this.memoryUsage.Value;
                this.memoryUsage = null;
                return value;
            },
            unit: "By",
            description: "The amount of physical memory allocated for this process.");

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () =>
            {
                if (!this.virtualMemoryUsage.HasValue)
                {
                    this.Snapshot();
                }

                var value = this.virtualMemoryUsage.Value;
                this.virtualMemoryUsage = null;
                return value;
            },
            unit: "By",
            description: "The amount of virtual memory allocated for this process that cannot be shared with other processes.");

        this.MeterInstance.CreateObservableCounter(
            "process.cpu.time",
            () =>
            {
                if (!this.userProcessorTime.HasValue || !this.privilegedProcessorTime.HasValue)
                {
                    this.Snapshot();
                }

                var userProcessorTimeValue = this.userProcessorTime.Value;
                var privilegedProcessorTimeValue = this.privilegedProcessorTime.Value;
                this.userProcessorTime = null;
                this.privilegedProcessorTime = null;

                return new[]
                {
                    new Measurement<double>(userProcessorTimeValue, new KeyValuePair<string, object?>("state", "user")),
                    new Measurement<double>(privilegedProcessorTimeValue, new KeyValuePair<string, object?>("state", "system")),
                };
            },
            unit: "s",
            description: "Total CPU seconds broken down by different states.");

        this.MeterInstance.CreateObservableGauge(
            "process.cpu.utilization",
            () =>
            {
                if (this.cpuUtilization == null)
                {
                    this.Snapshot();
                }

                var value = this.cpuUtilization;
                this.cpuUtilization = null;
                return value;
            },
            unit: "1",
            description: "Difference in process.cpu.time since the last measurement, divided by the elapsed time and number of CPUs available to the process.");

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.threads",
            () =>
            {
                if (!this.numberOfThreads.HasValue)
                {
                    this.Snapshot();
                }

                var value = this.numberOfThreads.Value;
                this.numberOfThreads = null;
                return value;
            },
            unit: "{threads}",
            description: "Process threads count.");
    }

    private void Snapshot()
    {
        this.currentProcess.Refresh();
        this.memoryUsage = this.currentProcess.WorkingSet64;
        this.virtualMemoryUsage = this.currentProcess.PrivateMemorySize64;
        this.userProcessorTime = this.currentProcess.UserProcessorTime.TotalSeconds;
        this.privilegedProcessorTime = this.currentProcess.PrivilegedProcessorTime.TotalSeconds;
        this.cpuUtilization = this.GetCpuUtilization();
        this.numberOfThreads = this.currentProcess.Threads.Count;
    }

    private IEnumerable<Measurement<double>> GetCpuUtilization()
    {
        var userProcessorUtilization = (this.userProcessorTime - this.lastCollectionUserProcessorTime) /
                ((DateTime.UtcNow - this.lastCollectionTimeStamp.ToUniversalTime()).TotalSeconds * Environment.ProcessorCount);

        var privilegedProcessorUtilization = (this.privilegedProcessorTime - this.lastCollectionPrivilegedProcessorTime) /
                ((DateTime.UtcNow - this.lastCollectionTimeStamp.ToUniversalTime()).TotalSeconds * Environment.ProcessorCount);

        this.lastCollectionTimeStamp = DateTime.UtcNow;
        this.lastCollectionUserProcessorTime = this.currentProcess.UserProcessorTime.TotalSeconds;
        this.lastCollectionPrivilegedProcessorTime = this.currentProcess.PrivilegedProcessorTime.TotalSeconds;

        return new[]
        {
            new Measurement<double>(userProcessorUtilization.Value, new KeyValuePair<string, object?>("state", "user")),
            new Measurement<double>(privilegedProcessorUtilization.Value, new KeyValuePair<string, object?>("state", "system")),
        };
    }
}
