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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
#if NETCOREAPP3_1_OR_GREATER
using System.Threading;
#endif

namespace OpenTelemetry.Instrumentation.Runtime
{
    /// <summary>
    /// .NET runtime instrumentation.
    /// </summary>
    internal class RuntimeMetrics : IDisposable
    {
        internal static readonly AssemblyName AssemblyName = typeof(RuntimeMetrics).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();
        private const long NanosecondsPerTick = 100;
        private static readonly string[] GenNames = new string[] { "gen0", "gen1", "gen2", "loh", "poh" };
        private static readonly int NumberOfGenerations = 3;
        private static string metricPrefix = "process.runtime.dotnet.";
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
                // TODO: Almost all the ObservableGauge should be ObservableUpDownCounter (except for CPU utilization and fragmentation.ratio.
                // Replace them once ObservableUpDownCounter is available.
                this.meter.CreateObservableGauge($"{metricPrefix}gc.heap", () => GC.GetTotalMemory(false), "By", "GC Heap Size.");
                this.meter.CreateObservableGauge($"{metricPrefix}gc.count", () => GetGarbageCollectionCounts(), description: "GC Count for all generations.");

#if NETCOREAPP3_1_OR_GREATER
                this.meter.CreateObservableCounter($"{metricPrefix}gc.allocated.bytes", () => GC.GetTotalAllocatedBytes(), "By", "Allocation Rate.");
                this.meter.CreateObservableGauge($"{metricPrefix}gc.fragmentation.ratio", GetFragmentation, description: "GC Fragmentation.");
#endif

#if NET6_0_OR_GREATER
                this.meter.CreateObservableGauge($"{metricPrefix}gc.committed", () => GC.GetGCMemoryInfo().TotalCommittedBytes, "By", description: "GC Committed Bytes.");
                this.meter.CreateObservableGauge($"{metricPrefix}gc.heapsize", () => GetGarbageCollectionHeapSizes(), "By", "Heap size for all generations.");
#endif
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.meter.CreateObservableCounter($"{metricPrefix}il.bytes.jitted", () => System.Runtime.JitInfo.GetCompiledILBytes(), "By", description: "IL Bytes Jitted.");
                this.meter.CreateObservableCounter($"{metricPrefix}methods.jitted.count", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Number of Methods Jitted.");
                this.meter.CreateObservableGauge($"{metricPrefix}time.in.jit", () => System.Runtime.JitInfo.GetCompilationTime().Ticks * NanosecondsPerTick, "ns", description: "Time spent in JIT.");
            }
#endif

#if NETCOREAPP3_1_OR_GREATER
            if (options.IsThreadingEnabled)
            {
                this.meter.CreateObservableGauge($"{metricPrefix}monitor.lock.contention.count", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count.");
                this.meter.CreateObservableGauge($"{metricPrefix}threadpool.thread.count", () => (long)ThreadPool.ThreadCount, description: "ThreadPool Thread Count.");
                this.meter.CreateObservableGauge($"{metricPrefix}threadpool.completed.items.count", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool Completed Work Item Count.");
                this.meter.CreateObservableGauge($"{metricPrefix}threadpool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool Queue Length.");
                this.meter.CreateObservableGauge($"{metricPrefix}active.timer.count", () => Timer.ActiveCount, description: "Number of Active Timers.");
            }
#endif

            if (options.IsProcessEnabled)
            {
                this.meter.CreateObservableCounter("process.cpu.time", GetProcessorTimes, "s", "Processor time of this process.");

                // Not yet official: https://github.com/open-telemetry/opentelemetry-specification/pull/2392
                this.meter.CreateObservableGauge("process.cpu.count", () => (long)Environment.ProcessorCount, description: "The number of available logical CPUs.");
                this.meter.CreateObservableGauge("process.memory.usage", () => Process.GetCurrentProcess().WorkingSet64, "By", "The amount of physical memory in use.");
                this.meter.CreateObservableGauge("process.memory.virtual", () => Process.GetCurrentProcess().VirtualMemorySize64, "By", "The amount of committed virtual memory.");
            }

            if (options.IsAssembliesEnabled)
            {
                this.meter.CreateObservableCounter($"{metricPrefix}assembly.count", () => (long)AppDomain.CurrentDomain.GetAssemblies().Length, description: "Number of Assemblies Loaded.");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.meter?.Dispose();
        }

        private static IEnumerable<Measurement<long>> GetGarbageCollectionCounts()
        {
            for (int i = 0; i < NumberOfGenerations; ++i)
            {
                yield return new(GC.CollectionCount(i), new KeyValuePair<string, object>("gen", GenNames[i]));
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        private static double GetFragmentation()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 1.0d / gcInfo.HeapSizeBytes : 0;
        }
#endif

#if NET6_0_OR_GREATER
        private static IEnumerable<Measurement<long>> GetGarbageCollectionHeapSizes()
        {
            var generationInfo = GC.GetGCMemoryInfo().GenerationInfo;

            Measurement<long>[] measurements = new Measurement<long>[generationInfo.Length];
            int maxSupportedLength = Math.Min(generationInfo.Length, GenNames.Length);
            for (int i = 0; i < maxSupportedLength; ++i)
            {
                measurements[i] = new(generationInfo[i].SizeAfterBytes, new KeyValuePair<string, object>("gen", GenNames[i]));
            }

            return measurements;
        }
#endif

        private static IEnumerable<Measurement<double>> GetProcessorTimes()
        {
            var process = Process.GetCurrentProcess();
            yield return new(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object>("state", "user"));
            yield return new(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object>("state", "system"));
        }
    }
}
