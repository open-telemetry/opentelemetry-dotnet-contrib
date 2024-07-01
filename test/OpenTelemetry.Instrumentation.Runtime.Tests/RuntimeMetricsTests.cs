// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Runtime.Tests;

public class RuntimeMetricsTests
{
    private const int MaxTimeToAllowForFlush = 10000;

    [Fact]
    public void RuntimeMetricsAreCaptured()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddRuntimeInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // The process.runtime.dotnet.exception.count metrics are only available after an exception has been thrown post OpenTelemetry.Instrumentation.Runtime initialization.
        try
        {
            throw new Exception("Oops!");
        }
        catch (Exception)
        {
            // swallow the exception
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.True(exportedItems.Count > 1);

        var assembliesCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.assemblies.count");
        Assert.NotNull(assembliesCountMetric);

        var exceptionsCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.exceptions.count");
        Assert.NotNull(exceptionsCountMetric);
        Assert.True(GetValue(exceptionsCountMetric) >= 1);
    }

    [Fact]
    public void GcMetricsTest()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddRuntimeInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        GC.Collect(1);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        var gcCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.collections.count");
        Assert.NotNull(gcCountMetric);

        var totalObjectsSize = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.objects.size");
        Assert.NotNull(totalObjectsSize);

#if NET

        var gcAllocationSizeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.allocations.size");
        Assert.NotNull(gcAllocationSizeMetric);

        var gcCommittedMemorySizeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.committed_memory.size");
        Assert.NotNull(gcCommittedMemorySizeMetric);

        var gcHeapSizeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.heap.size");
        Assert.NotNull(gcHeapSizeMetric);

        if (Environment.Version.Major >= 7)
        {
            var gcHeapFragmentationSizeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.heap.fragmentation.size");
            Assert.NotNull(gcHeapFragmentationSizeMetric);

            var gcDurationMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.gc.duration");
            Assert.NotNull(gcDurationMetric);
        }
#endif
    }

#if NET
    [Fact]
    public void JitRelatedMetricsTest()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
             .AddRuntimeInstrumentation()
             .AddInMemoryExporter(exportedItems)
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        var jitCompiledSizeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.jit.il_compiled.size");
        Assert.NotNull(jitCompiledSizeMetric);

        var jitMethodsCompiledCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.jit.methods_compiled.count");
        Assert.NotNull(jitMethodsCompiledCountMetric);

        var jitCompilationTimeMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.jit.compilation_time");
        Assert.NotNull(jitCompilationTimeMetric);
    }

    [Fact]
    public async Task ThreadingRelatedMetricsTest()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddRuntimeInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Bump the count for `thread_pool.completed_items.count` metric
        int taskCount = 50;
        List<Task> tasks = new List<Task>();
        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(() => { }));
        }

        await Task.WhenAll(tasks);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        var lockContentionCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.monitor.lock_contention.count");
        Assert.NotNull(lockContentionCountMetric);

        var threadCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.thread_pool.threads.count");
        Assert.NotNull(threadCountMetric);

        var completedItemsCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.thread_pool.completed_items.count");
        Assert.NotNull(completedItemsCountMetric);
        Assert.True(GetValue(completedItemsCountMetric) >= taskCount);

        var queueLengthMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.thread_pool.queue.length");
        Assert.NotNull(queueLengthMetric);

        List<Timer> timers = new List<Timer>();
        try
        {
            // Create 10 timers to bump timer.count metrics.
            int timerCount = 10;
            TimerCallback timerCallback = _ => { };
            for (int i = 0; i < timerCount; i++)
            {
                Timer timer = new Timer(timerCallback, null, 1000, 250);
                timers.Add(timer);
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);

            var timerCountMetric = exportedItems.FirstOrDefault(i => i.Name == "process.runtime.dotnet.timer.count");
            Assert.NotNull(timerCountMetric);
            Assert.True(GetValue(timerCountMetric) >= timerCount);
        }
        finally
        {
            for (int i = 0; i < timers.Count; i++)
            {
                timers[i].Dispose();
            }
        }
    }
#endif

    private static double GetValue(Metric metric)
    {
        double sum = 0;

        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            if (metric.MetricType.IsSum())
            {
                sum += metricPoint.GetSumLong();
            }
            else
            {
                sum += metricPoint.GetGaugeLastValueLong();
                break;
            }
        }

        return sum;
    }
}
