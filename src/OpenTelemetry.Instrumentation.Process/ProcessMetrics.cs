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
using System.Diagnostics.Metrics;
using System.Reflection;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    static ProcessMetrics()
    {
        // Refer to the spec for instrument details:
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/process-metrics.md#metric-instruments

        InstrumentsValues values = new InstrumentsValues(() => (-1, -1, -1, -1));

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => values.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use by the current process.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => values.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory by the current process.");

        MeterInstance.CreateObservableCounter(
            $"process.cpu.time",
            () => values.GetCpuTime(),
            unit: "s",
            description: "The amount of time that the current process has actively used a CPU to perform computations.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            $"process.cpu.utilization",
            () => values.GetCpuUtilization(),
            unit: "s",
            description: "Difference in process.cpu.time since the last collection of observerble instruments from the MetricReader, divided by the elapsed time multiply by the number of CPUs available to the current process.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }

    private class InstrumentsValues
    {
        private const int TicksPerSecond = 10 * 7;
        private readonly Diagnostics.Process currentProcess = Diagnostics.Process.GetCurrentProcess();

        private double? memoryUsage;
        private double? virtualMemoryUsage;
        private double? cpuTime;
        private double? cpuUtlization;

        private DateTime lastCollectionTimestamp = DateTime.Now;
        private double lastCollectionCpuTime;

        public InstrumentsValues(Func<(double MemoryUsage, double VirtualMemoryUsage, double CpuTime, double CpuUtilization)> readValues)
        {
            this.memoryUsage = null;
            this.virtualMemoryUsage = null;
            this.cpuTime = null;
            this.cpuUtlization = null;

            this.currentProcess.Refresh();
            this.UpdateValues = readValues;
        }

        private Func<(double MemoryUsage, double VirtualMemoryUsage, double CpuTime, double CpuUtilization)> UpdateValues { get; set; }

        public double GetMemoryUsage()
        {
            if (!this.memoryUsage.HasValue)
            {
                (this.memoryUsage, this.virtualMemoryUsage, this.cpuTime, this.cpuUtlization) = this.UpdateValues();
            }

            var value = this.currentProcess.WorkingSet64;
            this.memoryUsage = null;
            return value;
        }

        public double GetVirtualMemoryUsage()
        {
            if (!this.virtualMemoryUsage.HasValue)
            {
                (this.memoryUsage, this.virtualMemoryUsage, this.cpuTime, this.cpuUtlization) = this.UpdateValues();
            }

            var value = this.currentProcess.VirtualMemorySize64;
            this.virtualMemoryUsage = null;
            return value;
        }

        public double GetCpuTime()
        {
            if (!this.cpuTime.HasValue)
            {
                (this.memoryUsage, this.virtualMemoryUsage, this.cpuTime, this.cpuUtlization) = this.UpdateValues();
            }

            var value = this.currentProcess.TotalProcessorTime.Ticks * TicksPerSecond;
            this.cpuTime = null;
            return value;
        }

        public double GetCpuUtilization()
        {
            if (!this.cpuUtlization.HasValue)
            {
                (this.memoryUsage, this.virtualMemoryUsage, this.cpuTime, this.cpuUtlization) = this.UpdateValues();
            }

            var elapsedTime = (DateTime.Now - this.lastCollectionTimestamp).Ticks * TicksPerSecond;
            var currentCpuTime = this.currentProcess.TotalProcessorTime.Ticks * TicksPerSecond;
            var deltaInCpuTime = currentCpuTime - this.lastCollectionCpuTime;

            this.lastCollectionTimestamp = DateTime.Now;
            this.lastCollectionCpuTime = currentCpuTime;

            var value = deltaInCpuTime / (Environment.ProcessorCount * elapsedTime);
            this.cpuUtlization = null;
            return value;
        }
    }
}
