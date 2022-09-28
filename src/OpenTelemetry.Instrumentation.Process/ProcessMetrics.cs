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
using System.Threading;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    private static readonly ThreadLocal<InstrumentsValues> CurrentThreadInstrumentsValues = new(() => new InstrumentsValues());

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => CurrentThreadInstrumentsValues.Value.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => CurrentThreadInstrumentsValues.Value.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory.");
    }

    private class InstrumentsValues
    {
        private static readonly Diagnostics.Process CurrentProcess = Diagnostics.Process.GetCurrentProcess();
        private double? memoryUsage;
        private double? virtualMemoryUsage;

        internal InstrumentsValues()
        {
            this.memoryUsage = null;
            this.virtualMemoryUsage = null;
        }

        internal double GetMemoryUsage()
        {
            if (!this.memoryUsage.HasValue)
            {
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} invoke GetMemoryUsage() without the latest snapshot.");
                this.Snapshot();
            }

            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} retrieve memory usage from snapshot.");
            var value = (double)this.memoryUsage;
            this.memoryUsage = null;
            return value;
        }

        internal double GetVirtualMemoryUsage()
        {
            if (!this.virtualMemoryUsage.HasValue)
            {
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} invoke GetVirtualMemoryUsage() without the latest snapshot.");
                this.Snapshot();
            }

            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} retrieve virtual memory usage from snapshot.");

            var value = (double)this.virtualMemoryUsage;
            this.virtualMemoryUsage = null;
            return value;
        }

        private void Snapshot()
        {
            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} Refresh()");
            CurrentProcess.Refresh();
            this.memoryUsage = CurrentProcess.WorkingSet64;
            this.virtualMemoryUsage = CurrentProcess.PagedMemorySize64;
        }
    }
}
