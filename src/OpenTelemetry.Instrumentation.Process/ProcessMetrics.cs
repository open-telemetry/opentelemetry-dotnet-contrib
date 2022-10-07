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

internal sealed class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    private readonly Diagnostics.Process currentProcess = Diagnostics.Process.GetCurrentProcess();
    private double? memoryUsage;
    private double? virtualMemoryUsage;

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
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
    }

    private void Snapshot()
    {
        this.currentProcess.Refresh();
        this.memoryUsage = this.currentProcess.WorkingSet64;
        this.virtualMemoryUsage = this.currentProcess.PrivateMemorySize64;
    }
}
