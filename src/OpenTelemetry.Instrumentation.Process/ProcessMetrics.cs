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

    private const string MetricPrefix = "process.dotnet.";

    static ProcessMetrics()
    {
        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            $"{MetricPrefix}physical.memory.usage",
            () => (long)Diagnostics.Process.GetCurrentProcess().WorkingSet64,
            unit: "bytes",
            description: "The amount of physical memory allocated for the current process.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            $"{MetricPrefix}virtual.memory.usage",
            () => (long)Diagnostics.Process.GetCurrentProcess().VirtualMemorySize64,
            unit: "bytes",
            description: "The amount of virtual memory allocated for the current process.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }
}
