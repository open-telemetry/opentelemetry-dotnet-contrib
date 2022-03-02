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
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gc.heap", () => GC.GetTotalMemory(false), "B", "GC Heap Size");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gen_0-gc.count", () => GC.CollectionCount(0), description: "Gen 0 GC Count");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gen_1-gc.count", () => GC.CollectionCount(1), description: "Gen 1 GC Count");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}gen_2-gc.count", () => GC.CollectionCount(2), description: "Gen 2 GC Count");
#if NETCOREAPP3_1_OR_GREATER
                this.meter.CreateObservableCounter($"{options.MetricPrefix}alloc.rate", () => GC.GetTotalAllocatedBytes(), "B", "Allocation Rate");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}gc.fragmentation", GetFragmentation, description: "GC Fragmentation");
#endif

#if NET6_0_OR_GREATER
                this.meter.CreateObservableCounter($"{options.MetricPrefix}gc.committed", () => (double)(GC.GetGCMemoryInfo().TotalCommittedBytes / 1_000_000), "MB", description: "GC Committed Bytes");
#endif
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.meter.CreateObservableCounter($"{options.MetricPrefix}il.bytes.jitted", () => System.Runtime.JitInfo.GetCompiledILBytes(), "B", description: "IL Bytes Jitted");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}methods.jitted.count", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Number of Methods Jitted");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}time.in.jit", () => System.Runtime.JitInfo.GetCompilationTime().TotalMilliseconds, "ms", description: "Time spent in JIT");
            }
#endif

#if NETCOREAPP3_1_OR_GREATER
            if (options.IsThreadingEnabled)
            {
                this.meter.CreateObservableGauge($"{options.MetricPrefix}monitor.lock.contention.count", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}threadpool.thread.count", () => ThreadPool.ThreadCount, description: "ThreadPool Thread Count");
                this.meter.CreateObservableGauge($"{options.MetricPrefix}threadpool.completed.items.count", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool Completed Work Item Count");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}threadpool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool Queue Length");
                this.meter.CreateObservableCounter($"{options.MetricPrefix}active.timer.count", () => Timer.ActiveCount, description: "Number of Active Timers");
            }
#endif

            if (options.IsMemoryEnabled)
            {
                this.meter.CreateObservableGauge($"{options.MetricPrefix}memory.usage", () => (double)(Environment.WorkingSet / 1_000_000), "MB", "Working Set");
            }

            if (options.IsAssembliesEnabled)
            {
                this.meter.CreateObservableCounter($"{options.MetricPrefix}assembly.count", () => AppDomain.CurrentDomain.GetAssemblies().Length, description: "Number of Assemblies Loaded");
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
    }
}
