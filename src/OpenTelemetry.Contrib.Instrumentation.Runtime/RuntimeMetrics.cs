// <copyright file="RuntimeMetrics.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime
{
    /// <summary>
    /// .NET runtime instrumentation.
    /// </summary>
    internal class RuntimeMetrics : IDisposable
    {
        internal static readonly AssemblyName AssemblyName = typeof(RuntimeMetrics).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();
        private static string metricPrefix = "process.runtime.dotnet.";
        private readonly Meter meter;
        private double previousProcessorTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeMetrics"/> class.
        /// </summary>
        /// <param name="options">The options to define the metrics.</param>
        public RuntimeMetrics(RuntimeMetricsOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);

            if (options.IsGcEnabled)
            {
                this.meter.CreateObservableGauge($"{metricPrefix}gc.heap", () => GC.GetTotalMemory(false), "B", "GC Heap Size");
                this.meter.CreateObservableGauge($"{metricPrefix}gen_0-gc.count", () => GC.CollectionCount(0), description: "Gen 0 GC Count");
                this.meter.CreateObservableGauge($"{metricPrefix}gen_1-gc.count", () => GC.CollectionCount(1), description: "Gen 1 GC Count");
                this.meter.CreateObservableGauge($"{metricPrefix}gen_2-gc.count", () => GC.CollectionCount(2), description: "Gen 2 GC Count");
#if NETCOREAPP3_1_OR_GREATER
                this.meter.CreateObservableCounter($"{metricPrefix}alloc.rate", () => GC.GetTotalAllocatedBytes(), "B", "Allocation Rate");
                this.meter.CreateObservableCounter($"{metricPrefix}gc.fragmentation", GetFragmentation, description: "GC Fragmentation");
#endif

#if NET6_0_OR_GREATER
                this.meter.CreateObservableCounter($"{metricPrefix}gc.committed", () => (double)(GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000), "MB", description: "GC Committed Bytes");
#endif
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.meter.CreateObservableCounter($"{metricPrefix}il.bytes.jitted", () => System.Runtime.JitInfo.GetCompiledILBytes(), "B", description: "IL Bytes Jitted");
                this.meter.CreateObservableCounter($"{metricPrefix}methods.jitted.count", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Number of Methods Jitted");
                this.meter.CreateObservableGauge($"{metricPrefix}time.in.jit", () => System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, "ms", description: "Time spent in JIT");
            }
#endif

#if NETCOREAPP3_1_OR_GREATER
            if (options.IsThreadingEnabled)
            {
                this.meter.CreateObservableGauge($"{metricPrefix}monitor.lock.contention.count", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
                this.meter.CreateObservableCounter($"{metricPrefix}threadpool.thread.count", () => ThreadPool.ThreadCount, description: "ThreadPool Thread Count");
                this.meter.CreateObservableGauge($"{metricPrefix}threadpool.completed.items.count", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool Completed Work Item Count");
                this.meter.CreateObservableCounter($"{metricPrefix}threadpool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool Queue Length");
                this.meter.CreateObservableCounter($"{metricPrefix}active.timer.count", () => Timer.ActiveCount, description: "Number of Active Timers");
            }
#endif

            if (options.IsMemoryEnabled)
            {
                this.meter.CreateObservableGauge($"{metricPrefix}memory.usage", () => (double)(Environment.WorkingSet / 1_000_000), "MB", "Working Set");
            }

            if (options.IsCpuEnabled)
            {
                this.meter.CreateObservableGauge($"{metricPrefix}cpu.usage", this.CalculateCpuUsage, "CPU usage");
            }

            if (options.IsAssembliesEnabled)
            {
                this.meter.CreateObservableCounter($"{metricPrefix}assembly.count", () => AppDomain.CurrentDomain.GetAssemblies().Length, description: "Number of Assemblies Loaded");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.meter?.Dispose();
        }

#if NETCOREAPP3_1_OR_GREATER
        private static double GetFragmentation()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes : 0;
        }
#endif

        private double CalculateCpuUsage()
        {
            var cpuUsage = 0.0;

            var endTime = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;

            if (this.previousProcessorTime != 0)
            {
                cpuUsage = (endTime - this.previousProcessorTime) / (100 * Environment.ProcessorCount);
            }

            this.previousProcessorTime = endTime;

            return cpuUsage;
        }
    }
}
