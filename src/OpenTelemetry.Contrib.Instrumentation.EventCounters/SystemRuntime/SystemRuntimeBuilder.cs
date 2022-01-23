// <copyright file="SystemRuntimeBuilder.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.SystemRuntime
{
    internal class SystemRuntimeBuilder : ISystemRuntimeBuilder
    {
        private readonly EventSourceOption option;

        public SystemRuntimeBuilder(EventCountersOptions options)
        {
            this.option = options.AddEventSource(KnownEventSources.SystemRuntime);
        }

        public ISystemRuntimeBuilder WithCounters(params string[] counterNames)
        {
            this.option.WithCounters(counterNames);
            return this;
        }

        public ISystemRuntimeBuilder WithAll()
        {
            return this.WithAllocationRate()
                       .WithCpuUsage()
                       .WithExceptionCount()
                       .WithGCCommittedBytes()
                       .WithGCFragmentation()
                       .WithGCHeapSize()
                       .WithGen0GCCount()
                       .WithGen0Size()
                       .WithGen1GCCount()
                       .WithGen1Size()
                       .WithGen2GCCount()
                       .WithGen2Size()
                       .WithILBytesJitted()
                       .WithLOHSize()
                       .WithMethodJittedCount()
                       .WithMonitorLockContentionCount()
                       .WithNumberOfActiveTimers()
                       .WithNumberOfAssembliesLoaded()
                       .WithPercentOfTimeinGCSinceLastGC()
                       .WithPOHSize()
                       .WithThreadPoolCompletedWorkItemCount()
                       .WithThreadPoolQueueLength()
                       .WithThreadPoolThreadCount()
                       .WithWorkingSet();
        }

        public ISystemRuntimeBuilder WithAllocationRate(string? metricName = null)
        {
            this.option.With(
                "alloc-rate",
                "The number of bytes allocated per update interval",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithPercentOfTimeinGCSinceLastGC(string? metricName = null)
        {
            this.option.With(
                "time-in-gc",
                "The percent of time in GC since the last GC",
                MetricType.DoubleGauge,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithCpuUsage(string? metricName = null)
        {
            this.option.With(
                "cpu-usage",
                "The percent of the process's CPU usage relative to all of the system CPU resources",
                MetricType.DoubleGauge,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithExceptionCount(string? metricName = null)
        {
            this.option.With(
               "exception-count",
               "The number of exceptions that have occurred",
               MetricType.LongSum,
               metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGCHeapSize(string? metricName = null)
        {
            this.option.With(
                "gc-heap-size",
                "The number of bytes thought to be allocated based on GC.GetTotalMemory(Boolean)",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGen0GCCount(string? metricName = null)
        {
            this.option.With(
                "gen-0-gc-count",
                "The number of times GC has occurred for Gen 0 per update interval",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGen0Size(string? metricName = null)
        {
            this.option.With(
                "gen-0-size",
                "The number of bytes for Gen 0 GC",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGen1GCCount(string? metricName = null)
        {
            this.option.With(
                "gen-1-gc-count",
                "The number of times GC has occurred for Gen 1 per update interval",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGen1Size(string? metricName = null)
        {
            this.option.With(
                "gen-1-size",
                "The number of bytes for Gen 1 GC",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGen2GCCount(string? metricName = null)
        {
            this.option.With(
                "gen-2-gc-count",
                "The number of times GC has occurred for Gen 2 per update interval",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGen2Size(string? metricName = null)
        {
            this.option.With(
                "gen-2-size",
                "The number of bytes for Gen 2 GC",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithLOHSize(string? metricName = null)
        {
            this.option.With(
                "loh-size",
                "The number of bytes for the large object heap",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithPOHSize(string? metricName = null)
        {
            this.option.With(
                "poh-size",
                "The number of bytes for the pinned object heap",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGCFragmentation(string? metricName = null)
        {
            this.option.With(
                "gc-fragmentation",
                "The GC Heap Fragmentation",
                MetricType.DoubleGauge,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithMonitorLockContentionCount(string? metricName = null)
        {
            this.option.With(
                "monitor-lock-contention-count",
                "The number of times there was contention when trying to take the monitor's lock, based on Monitor.LockContentionCount",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithNumberOfActiveTimers(string? metricName = null)
        {
            this.option.With(
                "active-timer-count",
                "The number of Timer instances that are currently active, based on Timer.ActiveCount",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithNumberOfAssembliesLoaded(string? metricName = null)
        {
            this.option.With(
                "assembly-count",
                "The number of Assembly instances loaded into a process at a point in time",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithThreadPoolCompletedWorkItemCount(string? metricName = null)
        {
            this.option.With(
                "threadpool-completed-items-count",
                "The number of work items that have been processed so far in the ThreadPool",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithThreadPoolQueueLength(string? metricName = null)
        {
            this.option.With(
                "threadpool-queue-length",
                "The number of work items that are currently queued to be processed in the ThreadPool",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithThreadPoolThreadCount(string? metricName = null)
        {
            this.option.With(
                "threadpool-thread-count",
                "The number of thread pool threads that currently exist in the ThreadPool, based on ThreadPool.ThreadCount",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithWorkingSet(string? metricName = null)
        {
            this.option.With(
                "working-set",
                "The amount of physical memory mapped to the process context at a point in time base on Environment.WorkingSet",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithILBytesJitted(string? metricName = null)
        {
            this.option.With(
                "il-bytes-jitted",
                "The total size of ILs that are JIT-compiled, in bytes",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithMethodJittedCount(string? metricName = null)
        {
            this.option.With(
                "method-jitted-count",
                "The number of methods that are JIT-compiled",
                MetricType.LongSum,
                metricName);

            return this;
        }

        public ISystemRuntimeBuilder WithGCCommittedBytes(string? metricName = null)
        {
            this.option.With(
               "gc-committed-bytes",
               "The number of bytes committed by the GC",
               MetricType.LongSum,
               metricName);

            return this;
        }
    }
}
