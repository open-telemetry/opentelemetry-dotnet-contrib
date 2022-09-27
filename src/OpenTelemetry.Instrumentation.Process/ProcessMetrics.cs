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

using System.Diagnostics.Metrics;
using System.Reflection;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
        InstrumentsValues values = new InstrumentsValues();

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => values.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => values.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory.");
    }

    private class InstrumentsValues
    {
        private readonly Diagnostics.Process currentProcess = Diagnostics.Process.GetCurrentProcess();

        private double? memoryUsage;
        private double? virtualMemoryUsage;

        public InstrumentsValues()
        {
            this.memoryUsage = null;
            this.virtualMemoryUsage = null;
        }

        public double GetMemoryUsage()
        {
            if (!this.memoryUsage.HasValue)
            {
                this.currentProcess.Refresh();
                this.memoryUsage = this.currentProcess.WorkingSet64;
            }

            double value = (double)this.memoryUsage;
            this.memoryUsage = null;
            return value;
        }

        public double GetVirtualMemoryUsage()
        {
            if (!this.virtualMemoryUsage.HasValue)
            {
                this.currentProcess.Refresh();
                this.virtualMemoryUsage = this.currentProcess.PagedMemorySize64;
            }

            double value = (double)this.virtualMemoryUsage;
            this.virtualMemoryUsage = null;
            return value;
        }
    }
}
