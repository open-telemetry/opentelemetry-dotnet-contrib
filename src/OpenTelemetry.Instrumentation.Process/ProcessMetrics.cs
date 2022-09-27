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
using System.Threading;
using Diagnostics = System.Diagnostics;

namespace OpenTelemetry.Instrumentation.Process;

internal class ProcessMetrics
{
    internal static readonly AssemblyName AssemblyName = typeof(ProcessMetrics).Assembly.GetName();
    internal static readonly Meter MeterInstance = new(AssemblyName.Name, AssemblyName.Version.ToString());

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
        ThreadSafeInstrumentValues threadSafeInstrumentValues = new();

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => threadSafeInstrumentValues.GetMemoryUsage(),
            unit: "By",
            description: "The amount of physical memory in use.");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.virtual",
            () => threadSafeInstrumentValues.GetVirtualMemoryUsage(),
            unit: "By",
            description: "The amount of committed virtual memory.");
    }

    private class ThreadSafeInstrumentValues
    {
        private readonly ThreadLocal<Dictionary<int, InstrumentsValues>> threadIdToInstrumentValues = new(() =>
        {
            return new Dictionary<int, InstrumentsValues>();
        });

        public ThreadSafeInstrumentValues()
        {
        }

        ~ThreadSafeInstrumentValues()
        {
            this.threadIdToInstrumentValues.Dispose();
        }

        public double GetMemoryUsage()
        {
            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} invoke ThreadSafeInstrumentValues GetMemoryUsage()");
            if (!this.threadIdToInstrumentValues.Value.ContainsKey(Environment.CurrentManagedThreadId))
            {
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} not in dictionary! inserting one entry for this thread...");

                this.threadIdToInstrumentValues.Value.Add(Environment.CurrentManagedThreadId, new InstrumentsValues());
            }

            this.threadIdToInstrumentValues.Value.TryGetValue(Environment.CurrentManagedThreadId, out var instrumentValues);
            return instrumentValues.GetMemoryUsage();
        }

        public double GetVirtualMemoryUsage()
        {
            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} invoke ThreadSafeInstrumentValues GetVirtualMemoryUsage()");
            if (!this.threadIdToInstrumentValues.Value.ContainsKey(Environment.CurrentManagedThreadId))
            {
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} not in dictionary! inserting one entry for this thread...");

                this.threadIdToInstrumentValues.Value.Add(Environment.CurrentManagedThreadId, new InstrumentsValues());
            }

            this.threadIdToInstrumentValues.Value.TryGetValue(Environment.CurrentManagedThreadId, out var instrumentsValues);
            return instrumentsValues.GetVirtualMemoryUsage();
        }
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
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} invoke GetMemoryUsage() without the latest snapshot.");
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} Refresh()");

                this.currentProcess.Refresh();
                this.memoryUsage = this.currentProcess.WorkingSet64;
                this.virtualMemoryUsage = this.currentProcess.PagedMemorySize64;
            }

            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} retrieve memory usage from snapshot.");
            double value = (double)this.memoryUsage;
            this.memoryUsage = null;
            return value;
        }

        public double GetVirtualMemoryUsage()
        {
            if (!this.virtualMemoryUsage.HasValue)
            {
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} invoke GetVirtualMemoryUsage() without the latest snapshot.");
                Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} Refresh()");

                this.currentProcess.Refresh();
                this.memoryUsage = this.currentProcess.WorkingSet64;
                this.virtualMemoryUsage = this.currentProcess.PagedMemorySize64;
            }

            Console.WriteLine($"threadId: {Environment.CurrentManagedThreadId.ToString()} retrieve virtual memory usage from snapshot.");

            double value = (double)this.virtualMemoryUsage;
            this.virtualMemoryUsage = null;
            return value;
        }
    }
}
