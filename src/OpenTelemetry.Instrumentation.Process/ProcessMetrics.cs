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
    private static readonly Diagnostics.Process CurrentProcess = Diagnostics.Process.GetCurrentProcess();

    static ProcessMetrics()
    {
        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () =>
            {
                CurrentProcess.Refresh();
                return CurrentProcess.WorkingSet64;
            },
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () =>
            {
                CurrentProcess.Refresh();
                return CurrentProcess.VirtualMemorySize64;
            },
            unit: "By",
            description: "The amount of committed virtual memory.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMetrics"/> class.
    /// </summary>
    /// <param name="options">The options to define the metrics.</param>
    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
        // TODO: change to ObservableUpDownCounter
        // MeterInstance.CreateObservableGauge(
        //    $"process.cpu.utilization",
        //    () => CurrentProcess.TotalProcessorTime.TotalMilliseconds / (Environment.ProcessorCount * (DateTime.Now - CurrentProcess.StartTime).Milliseconds),
        //    unit: "1",
        //    description: "Difference in process.cpu.time since the last measurement, divided by the elapsed time and number of CPUs available to the process.");

        // What type should be used for the label?
        // labels
        // state, if specified, SHOULD be one of: system, user, wait. A process SHOULD be characterized either by data points with no state labels, or only data points with state labels.
        // IEnumberable,
        // List<Measurement<long>>
        // wait time = totalprocessortime - Process.PrivilegedProcessorTime - Process.userProcessorTime

        if (options.ExpandOnCpuStates == true)
        {
           MeterInstance.CreateObservableCounter(
           $"process.cpu.time",
           () =>
           {
               Measurement<long>[] measurements = new Measurement<long>[3];

               CurrentProcess.Refresh();
               var priviledgedCpuTime = CurrentProcess.PrivilegedProcessorTime.Seconds;
               var userCpuTime = CurrentProcess.PrivilegedProcessorTime.Seconds;

               measurements[(int)CPUState.System] = new(priviledgedCpuTime, new KeyValuePair<string, object>("state", CPUState.System.ToString()));
               measurements[(int)CPUState.User] = new(userCpuTime, new KeyValuePair<string, object>("state", CPUState.User.ToString()));
               measurements[(int)CPUState.Wait] = new(CurrentProcess.TotalProcessorTime.Seconds - priviledgedCpuTime - userCpuTime, new KeyValuePair<string, object>("state", CPUState.Wait.ToString()));

               return measurements;
           },
           unit: "s",
           description: "Total CPU seconds broken down by different states.");
        }
        else
        {
           MeterInstance.CreateObservableCounter(
           $"process.cpu.time",
           () =>
           {
               CurrentProcess.Refresh();
               return CurrentProcess.TotalProcessorTime.Seconds;
           },
           unit: "s",
           description: "Total CPU seconds broken down by different states.");
        }

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => CurrentProcess.WorkingSet64,
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => CurrentProcess.VirtualMemorySize64,
            unit: "By",
            description: "The amount of committed virtual memory.");

    }

    private enum CPUState
    {
        System,
        User,
        Wait,
    }
}
