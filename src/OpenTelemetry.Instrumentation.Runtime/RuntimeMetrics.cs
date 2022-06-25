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
#if NET6_0_OR_GREATER
        private const long NanosecondsPerTick = 100;
#endif
        private static readonly string[] GenNames = new string[] { "gen0", "gen1", "gen2", "loh", "poh" };
        private static readonly int NumberOfGenerations = 3;
        private static bool isGcInfoAvailable;
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
                this.meter.CreateObservableCounter($"{metricPrefix}gc.collections.count", () => GetGarbageCollectionCounts(), description: "Number of times garbage collection has occurred since process start.");

#if NETCOREAPP3_1_OR_GREATER
                this.meter.CreateObservableCounter($"{metricPrefix}gc.allocations.size", () => GC.GetTotalAllocatedBytes(), unit: "By", description: "Count of the bytes allocated on the managed GC heap since the process start. .NET objects are allocated from this heap. Object allocations from unmanaged languages such as C/C++ do not use this heap.");
#endif

#if NET6_0_OR_GREATER
                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}gc.committed_memory.size", () => GetGarbageCollectionCommittedBytes(), unit: "By", description: "The amount of committed virtual memory for the managed GC heap, as observed during the latest garbage collection. Committed virtual memory may be larger than the heap size because it includes both memory for storing existing objects (the heap size) and some extra memory that is ready to handle newly allocated objects in the future. The value will be unavailable until garbage collection has occurred.");

                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}gc.heap.size", () => GetGarbageCollectionHeapSizes(), unit: "By", description: "The heap size (including fragmentation), as observed during the latest garbage collection. The value will be unavailable until garbage collection has occurred.");

                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}gc.heap.fragmentation.size", GetFragmentationSizes, unit: "By", description: "The heap fragmentation, as observed during the latest garbage collection. The value will be unavailable until garbage collection has occurred.");
#endif
            }

#if NET6_0_OR_GREATER
            if (options.IsJitEnabled)
            {
                this.meter.CreateObservableCounter($"{metricPrefix}jit.il_compiled", () => System.Runtime.JitInfo.GetCompiledILBytes(), unit: "By", description: "Count of bytes of intermediate language that have been compiled since the process start.");
                this.meter.CreateObservableCounter($"{metricPrefix}jit.methods_compiled", () => System.Runtime.JitInfo.GetCompiledMethodCount(), description: "Count of methods that have been compiled since the process start.");
                this.meter.CreateObservableCounter($"{metricPrefix}jit.compilation_time", () => System.Runtime.JitInfo.GetCompilationTime().Ticks * NanosecondsPerTick, unit: "ns", description: "The amount of time the JIT compiler has spent compiling methods since the process start.");
            }
#endif

#if NETCOREAPP3_1_OR_GREATER
            if (options.IsThreadingEnabled)
            {
                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}monitor.lock_contention.count", () => Monitor.LockContentionCount, description: "The number of times there was contention when trying to acquire a monitor lock since the process start. Monitor locks are commonly acquired by using the lock keyword in C#, or by calling Monitor.Enter() and Monitor.TryEnter()");

                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}thread_pool.threads.count", () => (long)ThreadPool.ThreadCount, description: "The number of thread pool threads that currently exist.");

                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}thread_pool.completed_items.count", () => ThreadPool.CompletedWorkItemCount, description: "The number of work items that have been processed by the thread pool since the process start.");

                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}thread_pool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "The number of work items that are currently queued to be processed by the thread pool.");

                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}timer.count", () => Timer.ActiveCount, description: "The number of timer instances that are currently active. Timers can be created by many sources such as System.Threading.Timer, Task.Delay, or the timeout in a CancellationSource. An active timer is registered to tick at some point in the future and has not yet been canceled.");
            }
#endif

            if (options.IsAssembliesEnabled)
            {
                // TODO: change to ObservableUpDownCounter
                this.meter.CreateObservableGauge($"{metricPrefix}assembly.count", () => (long)AppDomain.CurrentDomain.GetAssemblies().Length, description: "The number of .NET assemblies that are currently loaded.");
            }

            if (options.IsExceptionCountEnabled)
            {
                var exceptionCounter = this.meter.CreateCounter<long>($"{metricPrefix}exception.count", description: "Count of exceptions that have been thrown in managed code, since the observation started.");
                AppDomain.CurrentDomain.FirstChanceException += (source, e) =>
                {
                    exceptionCounter.Add(1);
                };
            }
        }

        private static bool IsGcInfoAvailable
        {
            get
            {
                if (isGcInfoAvailable)
                {
                    return true;
                }

                if (GC.CollectionCount(0) > 0)
                {
                    isGcInfoAvailable = true;
                }

                return isGcInfoAvailable;
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

#if NET6_0_OR_GREATER
        private static IEnumerable<Measurement<long>> GetFragmentationSizes()
        {
            if (!IsGcInfoAvailable)
            {
                return Array.Empty<Measurement<long>>();
            }

            var generationInfo = GC.GetGCMemoryInfo().GenerationInfo;
            Measurement<long>[] measurements = new Measurement<long>[generationInfo.Length];
            int maxSupportedLength = Math.Min(generationInfo.Length, GenNames.Length);
            for (int i = 0; i < maxSupportedLength; ++i)
            {
                measurements[i] = new(generationInfo[i].FragmentationAfterBytes, new KeyValuePair<string, object>("gen", GenNames[i]));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<long>> GetGarbageCollectionCommittedBytes()
        {
            if (!IsGcInfoAvailable)
            {
                return Array.Empty<Measurement<long>>();
            }

            return new Measurement<long>[] { new(GC.GetGCMemoryInfo().TotalCommittedBytes) };
        }

        private static IEnumerable<Measurement<long>> GetGarbageCollectionHeapSizes()
        {
            if (!IsGcInfoAvailable)
            {
                return Array.Empty<Measurement<long>>();
            }

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
    }
}
