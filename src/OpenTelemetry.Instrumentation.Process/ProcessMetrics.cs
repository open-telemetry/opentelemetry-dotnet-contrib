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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading.Tasks;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());
    private const int MeasurementWindowInSeconds = 1;

    static ProcessMetrics()
    {
        InstrumentsValues values = new InstrumentsValues();

        // Refer to the spec for instrument details:
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/process-metrics.md#metric-instruments

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
            () => values.GetCpuUtilization(MeasurementWindowInSeconds).Result,
            unit: "s",
            description: "Difference in process.cpu.time since the last measurement, divided by the elapsed time and number of CPUs available to the current process.");
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
        private readonly Diagnostics.Process currentProcess = Diagnostics.Process.GetCurrentProcess();

        public InstrumentsValues()
        {
            this.currentProcess.Refresh();
        }

        public long GetMemoryUsage()
        {
            return this.currentProcess.WorkingSet64;
        }

        public long GetVirtualMemoryUsage()
        {
            return this.currentProcess.VirtualMemorySize64;
        }

        public double GetCpuTime()
        {
            return this.currentProcess.TotalProcessorTime.TotalSeconds;
        }

        public async Task<double> GetCpuUtilization(int measurementWindowInSeconds)
        {
            var startCpuTime = this.currentProcess.TotalProcessorTime.TotalSeconds;
            Stopwatch timer = Stopwatch.StartNew();

            await Task.Delay(measurementWindowInSeconds * 1000).ConfigureAwait(false);

            var endCpuTime = this.currentProcess.TotalProcessorTime.TotalSeconds;
            timer.Stop();

            return (endCpuTime - startCpuTime) / (Environment.ProcessorCount * (timer.ElapsedMilliseconds / 1000));
        }
    }
}
