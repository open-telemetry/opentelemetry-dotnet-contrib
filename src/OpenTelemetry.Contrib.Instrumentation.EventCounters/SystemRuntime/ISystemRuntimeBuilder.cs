// <copyright file="ISystemRuntimeBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.SystemRuntime
{
    /// <summary>
    /// Event source option builder for System.Runtime event counters.
    /// </summary>
    public interface ISystemRuntimeBuilder
    {
        /// <summary>
        /// Add several event counters to the options.
        /// </summary>
        /// <param name="counterNames">Name of the counters to listen to.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithCounters(params string[] counterNames);

        /// <summary>
        /// Add all known event counters to the options.
        /// </summary>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithAll();

        /// <summary>
        /// Adds the event counter for the percent of time in GC since the last GC.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithPercentOfTimeinGCSinceLastGC(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes allocated per update interval.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithAllocationRate(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the percent of the process's CPU usage relative to all of the system CPU resources.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithCpuUsage(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of exceptions that have occurred.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithExceptionCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes thought to be allocated based on GC.GetTotalMemory(Boolean).
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGCHeapSize(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of times GC has occurred for Gen 0 per update interval.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGen0GCCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes for Gen 0 GC.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGen0Size(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of times GC has occurred for Gen 1 per update interval.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGen1GCCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes for Gen 1 GC.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGen1Size(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of times GC has occurred for Gen 2 per update interval.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGen2GCCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes for Gen 2 GC.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGen2Size(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes for the large object heap.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithLOHSize(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of bytes for the pinned object heap (available on .NET 5 and later versions).
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithPOHSize(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the GC Heap Fragmentation (available on .NET 5 and later versions).
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGCFragmentation(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of times there was contention when trying to take the monitor's lock, based on Monitor.LockContentionCount.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithMonitorLockContentionCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of Timer instances that are currently active, based on Timer.ActiveCount.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithNumberOfActiveTimers(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of Assembly instances loaded into a process at a point in time.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithNumberOfAssembliesLoaded(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of work items that have been processed so far in the ThreadPool.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithThreadPoolCompletedWorkItemCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of work items that are currently queued to be processed in the ThreadPool.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithThreadPoolQueueLength(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of work items that are currently queued to be processed in the ThreadPool.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithThreadPoolThreadCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the amount of physical memory mapped to the process context at a point in time base on Environment.WorkingSet.
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithWorkingSet(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the total size of ILs that are JIT-compiled, in bytes (available on .NET 5 and later versions).
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithILBytesJitted(string? metricName = null);

        /// <summary>
        /// Adds the event counter for the number of methods that are JIT-compiled (available on .NET 5 and later versions).
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithMethodJittedCount(string? metricName = null);

        /// <summary>
        /// Adds the event counter for number of bytes committed by the GC (available on .NET 6 and later versions).
        /// </summary>
        /// <param name="metricName">Optional name of the published metric. Otherwise the counter name will be used.</param>
        /// <returns>The builder instance to define event counters.</returns>
        ISystemRuntimeBuilder WithGCCommittedBytes(string? metricName = null);
    }
}
