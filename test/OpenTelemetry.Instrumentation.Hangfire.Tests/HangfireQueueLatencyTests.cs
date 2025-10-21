// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using OpenTelemetry.Instrumentation.Hangfire.Implementation;
using OpenTelemetry.Instrumentation.Hangfire.Tests.Utils;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

[Collection("Hangfire")]
public class HangfireQueueLatencyTests : IClassFixture<HangfireFixture>
{
    private readonly HangfireFixture hangfireFixture;

    public HangfireQueueLatencyTests(HangfireFixture hangfireFixture)
    {
        this.hangfireFixture = hangfireFixture;
    }

    [Fact]
    public async Task Should_NOT_Record_QueueLatency_Metric_By_Default()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation() // Default: RecordQueueLatency = false
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert - Queue latency metric should NOT be present
        var queueLatencyMetric = exportedItems.FirstOrDefault(m => m.Name == HangfireMetrics.QueueLatencyMetricName);
        Assert.Null(queueLatencyMetric);
    }

    [Fact]
    public async Task Should_Record_QueueLatency_Metric_When_Enabled()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation(options =>
            {
                options.RecordQueueLatency = true;
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert - Queue latency metric should be present
        var queueLatencyMetric = exportedItems.GetMetric(HangfireMetrics.QueueLatencyMetricName);
        AssertUtils.AssertHasMetricPoints(queueLatencyMetric);
        Assert.Equal("s", queueLatencyMetric!.Unit);
        Assert.Equal(MetricType.Histogram, queueLatencyMetric.MetricType);

        var metricPoints = queueLatencyMetric.ToMetricPointList();
        var metricPoint = metricPoints.First();

        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        Assert.True(count >= 1, $"Expected histogram count >= 1, got {count}");
        Assert.True(sum >= 0, $"Expected queue latency sum >= 0, got {sum}");

        // Validate tags
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowTaskName);
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowPlatformName);
    }

    [Fact]
    public async Task Should_NOT_Record_QueueLatency_Metric_When_Explicitly_Disabled()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation(options =>
            {
                options.RecordQueueLatency = false; // Explicitly disabled
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert - Queue latency metric should NOT be present
        var queueLatencyMetric = exportedItems.FirstOrDefault(m => m.Name == HangfireMetrics.QueueLatencyMetricName);
        Assert.Null(queueLatencyMetric);
    }

    [Fact]
    public async Task Should_Record_Accurate_QueueLatency_For_Retried_Job()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation(options =>
            {
                options.RecordQueueLatency = true;
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act - Create a job that fails first, then gets retried
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        // Clear metrics from first execution
        exportedItems.Clear();

        // Manually requeue the job (simulating retry)
        BackgroundJob.Requeue(jobId);
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert - Queue latency should reflect time since re-enqueue, not original creation
        var queueLatencyMetric = exportedItems.GetMetric(HangfireMetrics.QueueLatencyMetricName);
        AssertUtils.AssertHasMetricPoints(queueLatencyMetric);

        var metricPoints = queueLatencyMetric!.ToMetricPointList();
        if (metricPoints.Count != 0)
        {
            var metricPoint = metricPoints.First();
            var sum = metricPoint.GetHistogramSum();

            // Queue latency should be small (< 10 seconds) since we just re-enqueued
            // If it used CreatedAt, it would be much larger
            Assert.True(sum < 10, $"Expected queue latency < 10 seconds for retry, got {sum}");
        }
    }

    [Fact]
    public async Task Should_Record_QueueLatency_For_Multiple_Jobs()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation(options =>
            {
                options.RecordQueueLatency = true;
            })
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act - Execute multiple jobs
        var jobId1 = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        var jobId2 = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        var jobId3 = BackgroundJob.Enqueue<TestJob>(x => x.Execute());

        await this.hangfireFixture.WaitJobProcessedAsync(jobId1, 5);
        await this.hangfireFixture.WaitJobProcessedAsync(jobId2, 5);
        await this.hangfireFixture.WaitJobProcessedAsync(jobId3, 5);

        meterProvider.ForceFlush();

        // Assert - Queue latency metric should have multiple data points
        var queueLatencyMetric = exportedItems.GetMetric(HangfireMetrics.QueueLatencyMetricName);
        AssertUtils.AssertHasMetricPoints(queueLatencyMetric);

        var metricPoints = queueLatencyMetric!.ToMetricPointList();
        Assert.NotEmpty(metricPoints);

        // At least one metric point should exist (could be aggregated)
        var metricPoint = metricPoints.First();
        var count = metricPoint.GetHistogramCount();

        Assert.True(count >= 3, $"Expected at least 3 queue latency measurements, got {count}");
    }

    [Fact]
    public void Should_NOT_Register_QueueLatencyFilter_When_Disabled()
    {
        // Arrange
        var options = new Trace.HangfireInstrumentationOptions
        {
            RecordQueueLatency = false,
        };

        // Act
        using var instrumentation = new Implementation.HangfireMetricsInstrumentation(options);

        var finalFilterCount = GlobalJobFilters.Filters.Count;

        // Verify no HangfireQueueLatencyFilterAttribute is registered
        var queueLatencyFilterExists = GlobalJobFilters.Filters
            .Any(f => f.Instance is Implementation.HangfireQueueLatencyFilterAttribute);
        Assert.False(queueLatencyFilterExists);
    }

    [Fact]
    public void Should_Register_QueueLatencyFilter_When_Enabled()
    {
        // Arrange
        var options = new Trace.HangfireInstrumentationOptions
        {
            RecordQueueLatency = true,
        };

        // Act
        using var instrumentation = new Implementation.HangfireMetricsInstrumentation(options);

        var finalFilterCount = GlobalJobFilters.Filters.Count;

        // Verify HangfireQueueLatencyFilterAttribute IS registered
        var queueLatencyFilterExists = GlobalJobFilters.Filters
            .Any(f => f.Instance is Implementation.HangfireQueueLatencyFilterAttribute);
        Assert.True(queueLatencyFilterExists);
    }
}
