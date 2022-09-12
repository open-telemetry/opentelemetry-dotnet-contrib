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
using System.Threading;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    private static readonly ThreadLocal<long?> WorkingSet64 = new(() => null);
    private static readonly ThreadLocal<long?> VirtualMemory64 = new(() => null);
    private static readonly ThreadLocal<Diagnostics.Process> CurrentProcess = new(() => Diagnostics.Process.GetCurrentProcess());

    static ProcessMetrics()
    {
        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () =>
            {
                if (WorkingSet64.Value == null)
                {
                    GetMetricValues();
                }

                var result = WorkingSet64.Value.Value;
                WorkingSet64.Value = null;
                return result;
            },
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () =>
            {
                if (VirtualMemory64.Value == null)
                {
                    GetMetricValues();
                }

                var result = VirtualMemory64.Value.Value;
                VirtualMemory64.Value = null;
                return result;
            },
            unit: "By",
            description: "The amount of committed virtual memory.");
    }

    private static void GetMetricValues()
    {
        var process = CurrentProcess.Value;
        process.Refresh();
        WorkingSet64.Value = process.WorkingSet64;
        VirtualMemory64.Value = process.VirtualMemorySize64;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }
}
