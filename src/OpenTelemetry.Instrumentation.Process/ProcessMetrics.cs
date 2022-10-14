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
    private double? memoryUsageBytes;
    private double? virtualMemoryUsageBytes;
    private double? userProcessorTimeSeconds;
    private double? privilegedProcessorTimeSeconds;
    private int? numberOfThreads;
    private IEnumerable<Measurement<double>> cpuUtilization;

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
                if (!this.memoryUsageBytes.HasValue)
                {
                    this.Snapshot();
                }

                var value = this.memoryUsageBytes.Value;
                this.memoryUsageBytes = null;
                return value;
            },
            unit: "By",
            description: "The amount of physical memory allocated for this process.");

        // TODO: change to ObservableUpDownCounter
        this.MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () =>
            {
                if (!this.virtualMemoryUsageBytes.HasValue)
                {
                    this.Snapshot();
                }

                var value = this.virtualMemoryUsageBytes.Value;
                this.virtualMemoryUsageBytes = null;
                return value;
            },
            unit: "By",
            description: "The amount of virtual memory allocated for this process that cannot be shared with other processes.");

        this.MeterInstance.CreateObservableCounter(
            "process.cpu.time",
            () =>
            {
                if (!this.userProcessorTimeSeconds.HasValue || !this.privilegedProcessorTimeSeconds.HasValue)
                {
                    this.Snapshot();
                }

                var userProcessorTimeSecondsValue = this.userProcessorTimeSeconds.Value;
                var privilegedProcessorTimeSecondsValue = this.privilegedProcessorTimeSeconds.Value;
                this.userProcessorTimeSeconds = null;
                this.privilegedProcessorTimeSeconds = null;

                return new[]
                {
                    new Measurement<double>(userProcessorTimeSecondsValue, new KeyValuePair<string, object?>("state", "user")),
                    new Measurement<double>(privilegedProcessorTimeSecondsValue, new KeyValuePair<string, object?>("state", "system")),
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
        this.memoryUsageBytes = this.currentProcess.WorkingSet64;
        this.virtualMemoryUsageBytes = this.currentProcess.PrivateMemorySize64;
        this.userProcessorTimeSeconds = this.currentProcess.UserProcessorTime.TotalSeconds;
        this.privilegedProcessorTimeSeconds = this.currentProcess.PrivilegedProcessorTime.TotalSeconds;
        this.cpuUtilization = this.GetCpuUtilization();
        this.numberOfThreads = this.currentProcess.Threads.Count;
    }

    private IEnumerable<Measurement<double>> GetCpuUtilization()
    {
        var temp = (DateTime.UtcNow - this.lastCollectionTimeUtc).TotalSeconds * Environment.ProcessorCount;
        var userProcessorUtilization = (this.userProcessorTimeSeconds - this.lastCollectedUserProcessorTime) / temp;
        var privilegedProcessorUtilization = (this.privilegedProcessorTimeSeconds - this.lastCollectedPrivilegedProcessorTime) / temp;

        this.lastCollectionTimeUtc = DateTime.UtcNow;
        this.lastCollectedUserProcessorTime = this.currentProcess.UserProcessorTime.TotalSeconds;
        this.lastCollectedPrivilegedProcessorTime = this.currentProcess.PrivilegedProcessorTime.TotalSeconds;

        return new[]
        {
            new Measurement<double>(Math.Min(userProcessorUtilization.Value, 1D), new KeyValuePair<string, object?>("state", "user")),
            new Measurement<double>(Math.Min(privilegedProcessorUtilization.Value, 1D), new KeyValuePair<string, object?>("state", "system")),
        };
    }
}
