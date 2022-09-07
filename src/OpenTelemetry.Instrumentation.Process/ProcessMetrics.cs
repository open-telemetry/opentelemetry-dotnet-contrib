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

        InstrumentsValues values = new InstrumentsValues(() => (-1, -1));

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

        private double? memoryUsage;
        private double? virtualMemoryUsage;

        public InstrumentsValues(Func<(double MemoryUsage, double VirtualMemoryUsage)> readValues)
        {
            this.memoryUsage = null;
            this.virtualMemoryUsage = null;

            this.currentProcess.Refresh();
            this.UpdateValues = readValues;
        }

        private Func<(double MemoryUsage, double VirtualMemoryUsage)> UpdateValues { get; set; }

        public double GetMemoryUsage()
        {
            if (!this.memoryUsage.HasValue)
            {
                (this.memoryUsage, this.virtualMemoryUsage) = this.UpdateValues();
            }

            var value = this.currentProcess.WorkingSet64;
            this.memoryUsage = null;
            return value;
        }

        public double GetVirtualMemoryUsage()
        {
            if (!this.virtualMemoryUsage.HasValue)
            {
                (this.memoryUsage, this.virtualMemoryUsage) = this.UpdateValues();
            }

            var value = this.currentProcess.VirtualMemorySize64;
            this.virtualMemoryUsage = null;
            return value;
        }
    }
}
