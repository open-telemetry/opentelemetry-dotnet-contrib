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

        private readonly Meter meter;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeMetrics"/> class.
        /// </summary>
        /// <param name="options">The options to define the metrics.</param>
        public RuntimeMetrics(RuntimeMetricsOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);

            if (options.IsGcEnabled)
            {
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gc_heap_size", () => (double)(GC.GetTotalMemory(false) / 1_000_000), "MB", "GC Heap Size");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gen_0-gc_count", () => GC.CollectionCount(0), description: "Gen 0 GC Count");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gen_1-gc_count", () => GC.CollectionCount(1), description: "Gen 1 GC Count");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gen_2-gc_count", () => GC.CollectionCount(2), description: "Gen 2 GC Count");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}alloc_rate", () => GC.GetTotalAllocatedBytes(), "B", "Allocation Rate");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}gc_fragmentation", GetFragmentation, description: "GC Fragmentation");

#if NET6_0_OR_GREATER
                this.meter.CreateObservableCounter($"{options.MetricPrefix}gc_committed", () => (double)(GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000), "MB", description: "GC Committed Bytes");
#endif
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.meter.CreateObservableCounter($"{options.MetricPrefix}il_bytes_jitted", () => System.Runtime.JitInfo.GetCompiledILBytes(), "B", description: "IL Bytes Jitted");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}methods_jitted_count", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Number of Methods Jitted");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}time_in_jit", () => System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, "ms", description: "Time spent in JIT");
            }
#endif

            if (options.IsThreadingEnabled)
            {
                this.meter.CreateObservableCounter($"{options.MetricPrefix}threadpool_thread_count", () => ThreadPool.ThreadCount, description: "ThreadPool Thread Count");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}monitor_lock_contention_count", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}threadpool_queue_length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool Queue Length");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}threadpool_completed_items_count", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool Completed Work Item Count");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}active_timer_count", () => Timer.ActiveCount, description: "Number of Active Timers");
            }

            if (options.IsPerformanceEnabled)
            {
                this.meter.CreateObservableGauge($"{options.MetricPrefix}working_set", () => (double)(Environment.WorkingSet / 1_000_000), "MB", "Working Set");
            }

            if (options.IsAssembliesEnabled)
            {
                this.meter.CreateObservableCounter($"{options.MetricPrefix}assembly_count", () => AppDomain.CurrentDomain.GetAssemblies().Length, description: "Number of Assemblies Loaded");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.meter?.Dispose();
        }

        private static double GetFragmentation()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes : 0;
        }
    }
}
