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

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => InstrumentsValues.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use by the current process.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => InstrumentsValues.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory by the current process.");

        MeterInstance.CreateObservableCounter(
            $"process.cpu.time",
            () => InstrumentsValues.GetCpuTime(),
            unit: "s",
            description: "The amount of time that the current process has actively used a CPU to perform computations.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            $"process.cpu.utilization",
            () => InstrumentsValues.GetCpuUtilization(),
            unit: "s",
            description: "Difference in process.cpu.time since the last measurement (collection of observerble instruments from MetricReader), divided by the elapsed time and number of CPUs available to the current process.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }

    private static class InstrumentsValues
    {
        private const int TicksPerSecond = 10 * 7;
        private static readonly Diagnostics.Process CurrentProcess = Diagnostics.Process.GetCurrentProcess();
        private static DateTime lastMeasurementTimestamp = CurrentProcess.StartTime;
        private static double lastMeasurementCpuTime;
        private static double currentMeasurementCpuTime;

        static InstrumentsValues()
        {
            CurrentProcess.Refresh();
        }

        internal static double GetMemoryUsage()
        {
            return CurrentProcess.WorkingSet64;
        }

        internal static double GetVirtualMemoryUsage()
        {
            return CurrentProcess.VirtualMemorySize64;
        }

        internal static double GetCpuTime()
        {
            currentMeasurementCpuTime = CurrentProcess.TotalProcessorTime.Ticks * TicksPerSecond;
            return currentMeasurementCpuTime;
        }

        internal static double GetCpuUtilization()
        {
            var elapsedTime = (DateTime.Now - lastMeasurementTimestamp).Ticks * TicksPerSecond;
            var deltaInCpuTime = currentMeasurementCpuTime - lastMeasurementCpuTime;

            lastMeasurementTimestamp = DateTime.Now;
            lastMeasurementCpuTime = currentMeasurementCpuTime;

            return deltaInCpuTime / (Environment.ProcessorCount * elapsedTime);
        }
    }
}
