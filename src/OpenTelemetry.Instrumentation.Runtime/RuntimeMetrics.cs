// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
#if NET
using System.Reflection;
#endif
#if NET
using JitInfo = System.Runtime.JitInfo;
#endif

namespace OpenTelemetry.Instrumentation.Runtime;

/// <summary>
/// .NET runtime instrumentation.
/// </summary>
internal sealed class RuntimeMetrics : IDisposable
{
    internal static readonly Meter MeterInstance = CreateMeterInstance();

#if NET
    private const long NanosecondsPerTick = 100;
    private const long GcMemoryInfoCacheDurationMilliseconds = 10;
#endif
    private const int NumberOfGenerations = 3;

    private static readonly string[] GenNames = ["gen0", "gen1", "gen2", "loh", "poh"];
    private static readonly Lock ExceptionSubscriptionSync = new();
    private static readonly Measurement<long>[] GarbageCollectionCountMeasurements = new Measurement<long>[NumberOfGenerations];
#if NET
    private static readonly Lock GcMemoryInfoCacheSync = new();
    private static readonly Measurement<long>[] GcHeapSizeMeasurementBuffer = new Measurement<long>[GenNames.Length];
    private static readonly Measurement<long>[] GcHeapFragmentationMeasurementBuffer = new Measurement<long>[GenNames.Length];
#endif
    private static readonly Counter<long> ExceptionCounter = MeterInstance.CreateCounter<long>(
        "process.runtime.dotnet.exceptions.count",
        description: "Count of exceptions that have been thrown in managed code, since the observation started. The value will be unavailable until an exception has been thrown after OpenTelemetry.Instrumentation.Runtime initialization.");

    private static readonly AssemblyLoadEventHandler AssemblyLoadHandler = static (_, _) =>
    {
        lock (ExceptionSubscriptionSync)
        {
            if (activeRuntimeMetricsInstances > 0)
            {
                Interlocked.Increment(ref loadedAssemblyCount);
            }
        }
    };

    private static readonly EventHandler<FirstChanceExceptionEventArgs> FirstChanceExceptionHandler = static (source, e) =>
    {
        ExceptionCounter.Add(1);
    };

    private static int activeRuntimeMetricsInstances;
    private static int loadedAssemblyCount;
#if NET
    private static GCMemoryInfo cachedGcMemoryInfo;
    private static long cachedGcMemoryInfoTickCount64 = long.MinValue;
#endif

    private bool disposed;

#pragma warning disable SA1313
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeMetrics"/> class.
    /// </summary>
    /// <param name="_1">The options to define the metrics.</param>
    public RuntimeMetrics(RuntimeInstrumentationOptions _1)
#pragma warning restore SA1313
    {
        lock (ExceptionSubscriptionSync)
        {
            if (activeRuntimeMetricsInstances++ == 0)
            {
                loadedAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
                AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoadHandler;
                AppDomain.CurrentDomain.FirstChanceException += FirstChanceExceptionHandler;
            }
        }
    }

    internal static int ActiveRuntimeMetricsInstances
    {
        get
        {
            lock (ExceptionSubscriptionSync)
            {
                return activeRuntimeMetricsInstances;
            }
        }
    }

#if NET
    private static bool IsGcInfoAvailable
    {
        get
        {
            if (field)
            {
                return true;
            }

            if (GC.CollectionCount(0) > 0)
            {
                field = true;
            }

            return field;
        }
    }
#endif

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (ExceptionSubscriptionSync)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            if (--activeRuntimeMetricsInstances == 0)
            {
                AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoadHandler;
                AppDomain.CurrentDomain.FirstChanceException -= FirstChanceExceptionHandler;
            }
        }
    }

    private static IEnumerable<Measurement<long>> GetGarbageCollectionCounts()
    {
        long collectionsFromHigherGeneration = 0;

        for (var gen = NumberOfGenerations - 1; gen >= 0; --gen)
        {
            long collectionsFromThisGeneration = GC.CollectionCount(gen);

            GarbageCollectionCountMeasurements[NumberOfGenerations - 1 - gen] = new(
                collectionsFromThisGeneration - collectionsFromHigherGeneration,
                new KeyValuePair<string, object?>("generation", GenNames[gen]));

            collectionsFromHigherGeneration = collectionsFromThisGeneration;
        }

        return GarbageCollectionCountMeasurements;
    }

    private static Meter CreateMeterInstance()
    {
        var meter = Metrics.MeterFactory.Create<RuntimeMetrics>(null); // These metrics are not in the Semantic Conventions

        meter.CreateObservableCounter(
            "process.runtime.dotnet.gc.collections.count",
            GetGarbageCollectionCounts,
            description: "Number of garbage collections that have occurred since process start.");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.gc.objects.size",
            () => GC.GetTotalMemory(false),
            unit: "bytes",
            description: "Count of bytes currently in use by objects in the GC heap that haven't been collected yet. Fragmentation and other GC committed memory pools are excluded.");

#if NET
        meter.CreateObservableCounter(
            "process.runtime.dotnet.gc.allocations.size",
            () => GC.GetTotalAllocatedBytes(),
            unit: "bytes",
            description: "Count of bytes allocated on the managed GC heap since the process start. .NET objects are allocated from this heap. Object allocations from unmanaged languages such as C/C++ do not use this heap.");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.gc.committed_memory.size",
            () =>
            {
                return !IsGcInfoAvailable ? Array.Empty<Measurement<long>>() : [new(GC.GetGCMemoryInfo().TotalCommittedBytes)];
            },
            unit: "bytes",
            description: "The amount of committed virtual memory for the managed GC heap, as observed during the latest garbage collection. Committed virtual memory may be larger than the heap size because it includes both memory for storing existing objects (the heap size) and some extra memory that is ready to handle newly allocated objects in the future. The value will be unavailable until at least one garbage collection has occurred.");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.gc.heap.size",
            GetGcHeapSizeMeasurements,
            unit: "bytes",
            description: "The heap size (including fragmentation), as observed during the latest garbage collection. The value will be unavailable until at least one garbage collection has occurred.");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.gc.heap.fragmentation.size",
            GetGcHeapFragmentationMeasurements,
            unit: "bytes",
            description: "The heap fragmentation, as observed during the latest garbage collection. The value will be unavailable until at least one garbage collection has occurred.");

        var mi = typeof(GC).GetMethod("GetTotalPauseDuration", BindingFlags.Public | BindingFlags.Static);
        var getTotalPauseDuration = mi?.CreateDelegate<Func<TimeSpan>>();
        if (getTotalPauseDuration != null)
        {
            meter.CreateObservableCounter(
                "process.runtime.dotnet.gc.duration",
                () => getTotalPauseDuration().Ticks * NanosecondsPerTick,
                unit: "ns",
                description: "The total amount of time paused in GC since the process start.");
        }

        meter.CreateObservableCounter(
            "process.runtime.dotnet.jit.il_compiled.size",
            () => JitInfo.GetCompiledILBytes(),
            unit: "bytes",
            description: "Count of bytes of intermediate language that have been compiled since the process start.");

        meter.CreateObservableCounter(
            "process.runtime.dotnet.jit.methods_compiled.count",
            () => JitInfo.GetCompiledMethodCount(),
            description: "The number of times the JIT compiler compiled a method since the process start. The JIT compiler may be invoked multiple times for the same method to compile with different generic parameters, or because tiered compilation requested different optimization settings.");

        meter.CreateObservableCounter(
            "process.runtime.dotnet.jit.compilation_time",
            () => JitInfo.GetCompilationTime().Ticks * NanosecondsPerTick,
            unit: "ns",
            description: "The amount of time the JIT compiler has spent compiling methods since the process start.");

        meter.CreateObservableCounter(
            "process.runtime.dotnet.monitor.lock_contention.count",
            () => Monitor.LockContentionCount,
            description: "The number of times there was contention when trying to acquire a monitor lock since the process start. Monitor locks are commonly acquired by using the lock keyword in C#, or by calling Monitor.Enter() and Monitor.TryEnter().");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.thread_pool.threads.count",
            () => (long)ThreadPool.ThreadCount,
            description: "The number of thread pool threads that currently exist.");

        meter.CreateObservableCounter(
            "process.runtime.dotnet.thread_pool.completed_items.count",
            () => ThreadPool.CompletedWorkItemCount,
            description: "The number of work items that have been processed by the thread pool since the process start.");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.thread_pool.queue.length",
            () => ThreadPool.PendingWorkItemCount,
            description: "The number of work items that are currently queued to be processed by the thread pool.");

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.timer.count",
            () => Timer.ActiveCount,
            description: "The number of timer instances that are currently active. Timers can be created by many sources such as System.Threading.Timer, Task.Delay, or the timeout in a CancellationSource. An active timer is registered to tick at some point in the future and has not yet been canceled.");
#endif

        meter.CreateObservableUpDownCounter(
            "process.runtime.dotnet.assemblies.count",
            GetAssemblyCount,
            description: "The number of .NET assemblies that are currently loaded.");

        return meter;
    }

    private static long GetAssemblyCount() => Volatile.Read(ref loadedAssemblyCount);

#if NET
    private static Measurement<long>[] GetGcHeapSizeMeasurements()
    {
        if (!IsGcInfoAvailable)
        {
            return [];
        }

        var generationInfo = GetCachedGcMemoryInfo().GenerationInfo;
        var maxSupportedLength = Math.Min(generationInfo.Length, GcHeapSizeMeasurementBuffer.Length);

        for (var i = 0; i < maxSupportedLength; ++i)
        {
            GcHeapSizeMeasurementBuffer[i] = new(
                generationInfo[i].SizeAfterBytes,
                new KeyValuePair<string, object?>("generation", GenNames[i]));
        }

        return GcHeapSizeMeasurementBuffer;
    }

    private static Measurement<long>[] GetGcHeapFragmentationMeasurements()
    {
        if (!IsGcInfoAvailable)
        {
            return [];
        }

        var generationInfo = GetCachedGcMemoryInfo().GenerationInfo;
        var maxSupportedLength = Math.Min(generationInfo.Length, GcHeapFragmentationMeasurementBuffer.Length);

        for (var i = 0; i < maxSupportedLength; ++i)
        {
            GcHeapFragmentationMeasurementBuffer[i] = new(
                generationInfo[i].FragmentationAfterBytes,
                new KeyValuePair<string, object?>("generation", GenNames[i]));
        }

        return GcHeapFragmentationMeasurementBuffer;
    }

    private static GCMemoryInfo GetCachedGcMemoryInfo()
    {
        var now = Environment.TickCount64;
        var cachedTickCount64 = Volatile.Read(ref cachedGcMemoryInfoTickCount64);

        if (cachedTickCount64 != long.MinValue &&
            (now - cachedTickCount64) <= GcMemoryInfoCacheDurationMilliseconds)
        {
            return cachedGcMemoryInfo;
        }

        lock (GcMemoryInfoCacheSync)
        {
            now = Environment.TickCount64;
            cachedTickCount64 = Volatile.Read(ref cachedGcMemoryInfoTickCount64);

            if (cachedTickCount64 == long.MinValue
                || (now - cachedTickCount64) > GcMemoryInfoCacheDurationMilliseconds)
            {
                cachedGcMemoryInfo = GC.GetGCMemoryInfo();
                Volatile.Write(ref cachedGcMemoryInfoTickCount64, now);
            }

            return cachedGcMemoryInfo;
        }
    }
#endif
}
