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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public ProcessMetrics(ProcessInstrumentationOptions? options)
    {
        InstrumentsValues values = new InstrumentsValues();

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => InstrumentsValues.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => InstrumentsValues.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory.");

        if (options.CpuStatesEnabled == false)
        {
            MeterInstance.CreateObservableCounter(
            $"process.cpu.time",
            () => InstrumentsValues.GetProcessorCpuTime(),
            unit: "s",
            description: "Total CPU seconds.");
        }
        else
        {
            MeterInstance.CreateObservableCounter(
            $"process.cpu.time",
            () => InstrumentsValues.GetProcessorCpuTimeWithBreakdown(),
            unit: "s",
            description: "Total CPU seconds broken down by different states.");
        }
    }

    private class InstrumentsValues
    {
        private static readonly Diagnostics.Process CurrentProcess = Diagnostics.Process.GetCurrentProcess();

        public InstrumentsValues()
        {
            CurrentProcess.Refresh();
        }

        private enum CPUState
        {
            System,
            User,
            Wait,
        }

        public static long GetMemoryUsage()
        {
            return CurrentProcess.WorkingSet64;
        }

        public static long GetVirtualMemoryUsage()
        {
            return CurrentProcess.VirtualMemorySize64;
        }

        public static long GetProcessorCpuTime()
        {
            return CurrentProcess.TotalProcessorTime.Seconds;
        }

        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/process-metrics.md#metric-instruments
        public static Measurement<long>[] GetProcessorCpuTimeWithBreakdown()
        {
            Measurement<long>[] measurements = new Measurement<long>[3];
            var priviledgedCpuTime = CurrentProcess.PrivilegedProcessorTime.Seconds;
            var userCpuTime = CurrentProcess.UserProcessorTime.Seconds;

            measurements[(int)CPUState.System] = new(priviledgedCpuTime, new KeyValuePair<string, object>("state", CPUState.System.ToString()));
            measurements[(int)CPUState.User] = new(userCpuTime, new KeyValuePair<string, object>("state", CPUState.User.ToString()));
            measurements[(int)CPUState.Wait] = new(CurrentProcess.TotalProcessorTime.Seconds - priviledgedCpuTime - userCpuTime, new KeyValuePair<string, object>("state", CPUState.Wait.ToString()));

            return measurements;
        }
    }
}
