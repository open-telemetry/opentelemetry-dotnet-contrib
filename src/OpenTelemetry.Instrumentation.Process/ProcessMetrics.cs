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
            () => values.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => values.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory.");

        if (options.CpuStatesEnabled == false)
        {
            MeterInstance.CreateObservableCounter(
            $"process.cpu.time",
            () => values.GetProcessorCpuTime(),
            unit: "s",
            description: "Total CPU seconds.");
        }
        else
        {
            MeterInstance.CreateObservableCounter(
            $"process.cpu.time",
            () => values.GetProcessorCpuTimeWithBreakdown(),
            unit: "s",
            description: "Total CPU seconds broken down by different states.");
        }
    }

    private class InstrumentsValues
    {
        private readonly Diagnostics.Process currentProcess = Diagnostics.Process.GetCurrentProcess();

        public InstrumentsValues()
        {
            this.currentProcess.Refresh();
        }

        private enum CpuState
        {
            System,
            User,
            Wait,
        }

        public long GetMemoryUsage()
        {
            return this.currentProcess.WorkingSet64;
        }

        public long GetVirtualMemoryUsage()
        {
            return this.currentProcess.VirtualMemorySize64;
        }

        public long GetProcessorCpuTime()
        {
            return this.currentProcess.TotalProcessorTime.Seconds;
        }

        // See spec:
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/process-metrics.md#metric-instruments
        public Measurement<long>[] GetProcessorCpuTimeWithBreakdown()
        {
            Measurement<long>[] measurements = new Measurement<long>[Enum.GetNames(typeof(CpuState)).Length];
            var priviledgedCpuTime = this.currentProcess.PrivilegedProcessorTime.Seconds;
            var userCpuTime = this.currentProcess.UserProcessorTime.Seconds;

            measurements[(int)CpuState.System] = new(priviledgedCpuTime, new KeyValuePair<string, object>("state", CpuState.System.ToString()));
            measurements[(int)CpuState.User] = new(userCpuTime, new KeyValuePair<string, object>("state", CpuState.User.ToString()));
            measurements[(int)CpuState.Wait] = new(this.currentProcess.TotalProcessorTime.Seconds - priviledgedCpuTime - userCpuTime, new KeyValuePair<string, object>("state", CpuState.Wait.ToString()));

            return measurements;
        }
    }
}
