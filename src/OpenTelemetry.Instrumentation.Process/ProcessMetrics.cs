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

    static ProcessMetrics()
    {
        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.usage",
            () => CurrentThreadInstrumentsValues.Value.GetMemoryUsage(),
            unit: "By",
            description: "The amount of workingSet64");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.pagedMemorySize",
            () => CurrentThreadInstrumentsValues.Value.GetPagedMemorySize(),
            unit: "By",
            description: "The amount of pagedMemorySize");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.pagedSystemMemorySize",
            () => CurrentThreadInstrumentsValues.Value.GetPagedSystemMemorySize(),
            unit: "By",
            description: "The amount of pagedSystemMemorySize");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.nonPagedSystemMemorySize",
            () => CurrentThreadInstrumentsValues.Value.GetNonPagedSystemMemorySize(),
            unit: "By",
            description: "The amount of nonPagedSystemMemorySize");

        // TODO: change to ObservableUpDownCounter
        MeterInstance.CreateObservableGauge(
            "process.memory.privateMemorySize",
            () => CurrentThreadInstrumentsValues.Value.GetPrivateMemorySize(),
            unit: "By",
            description: "The amount of PrivateMemorySize");
    }

    public ProcessMetrics(ProcessInstrumentationOptions options)
    {
    }

    private class InstrumentsValues
    {
        private static readonly Diagnostics.Process CurrentProcess = Diagnostics.Process.GetCurrentProcess();
        private double? workingSet64;
        private double? pagedMemorySize64;
        private double? pagedSystemMemorySize64;
        private double? nonpagedSystemMemorySize64;
        private double? privateMemorySize64;

        internal double GetMemoryUsage()
        {
            if (!this.workingSet64.HasValue)
            {
                this.Snapshot();
            }

            var value = this.workingSet64.Value;
            this.workingSet64 = null;
            return value;
        }

        internal double GetPagedMemorySize()
        {
            if (!this.pagedMemorySize64.HasValue)
            {
                this.Snapshot();
            }

            var value = this.pagedMemorySize64.Value;
            this.pagedMemorySize64 = null;
            return value;
        }

        internal double GetPagedSystemMemorySize()
        {
            if (!this.pagedSystemMemorySize64.HasValue)
            {
                this.Snapshot();
            }

            var value = this.pagedSystemMemorySize64.Value;
            this.pagedSystemMemorySize64 = null;
            return value;
        }

        internal double GetNonPagedSystemMemorySize()
        {
            if (!this.nonpagedSystemMemorySize64.HasValue)
            {
                this.Snapshot();
            }

            var value = this.nonpagedSystemMemorySize64.Value;
            this.nonpagedSystemMemorySize64 = null;
            return value;
        }

        internal double GetPrivateMemorySize()
        {
            if (!this.privateMemorySize64.HasValue)
            {
                this.Snapshot();
            }

            var value = this.privateMemorySize64.Value;
            this.privateMemorySize64 = null;
            return value;
        }

        private void Snapshot()
        {
            Console.WriteLine("Refresh()");
            CurrentProcess.Refresh();

            this.workingSet64 = CurrentProcess.WorkingSet64;
            this.pagedMemorySize64 = CurrentProcess.PagedMemorySize64;
            this.pagedSystemMemorySize64 = CurrentProcess.PagedSystemMemorySize64;
            this.nonpagedSystemMemorySize64 = CurrentProcess.NonpagedSystemMemorySize64;
            this.privateMemorySize64 = CurrentProcess.PrivateMemorySize64;
        }
    }
}
