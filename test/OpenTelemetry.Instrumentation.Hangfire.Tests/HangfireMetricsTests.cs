// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Hangfire;
using OpenTelemetry.Instrumentation.Hangfire.Implementation;
using OpenTelemetry.Instrumentation.Hangfire.Tests.Utils;
using OpenTelemetry.Metrics;
using Xunit;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

[Collection("Hangfire")]
public class HangfireMetricsTests : IClassFixture<HangfireFixture>
{
    private readonly HangfireFixture hangfireFixture;

    public HangfireMetricsTests(HangfireFixture hangfireFixture)
    {
        this.hangfireFixture = hangfireFixture;
    }

    [Fact]
    public async Task Should_Record_ExecutionCount_Metric_When_Job_Executes()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert
        var executionCountMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionCountMetricName);
        AssertUtils.AssertHasMetricPoints(executionCountMetric);
        Assert.Equal("{executions}", executionCountMetric!.Unit);
        Assert.Equal(MetricType.LongSum, executionCountMetric.MetricType);

        var metricPoints = executionCountMetric.ToMetricPointList();
        var sum = metricPoints.First().GetSumLong();
        Assert.True(sum >= 1, $"Expected workflow.execution.count sum >= 1, got {sum}");

        // Validate tags
        var metricPoint = metricPoints.First();
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowTaskName);
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowPlatformName);
        AssertUtils.AssertHasTagValue(metricPoint, HangfireTagBuilder.TagWorkflowExecutionOutcome, HangfireTagBuilder.OutcomeSuccess);
    }

    [Fact]
    public async Task Should_Record_ExecutionDuration_Metric_For_Successful_Job()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert
        var durationMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionDurationMetricName);
        AssertUtils.AssertHasMetricPoints(durationMetric);
        Assert.Equal("s", durationMetric!.Unit);
        Assert.Equal(MetricType.Histogram, durationMetric.MetricType);

        var metricPoints = durationMetric.ToMetricPointList();
        var metricPoint = metricPoints.First();

        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        Assert.True(count >= 1, $"Expected histogram count >= 1, got {count}");
        Assert.True(sum >= 0, $"Expected histogram sum >= 0, got {sum}");

        // Validate tags for successful execution
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowTaskName);
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowPlatformName);
        AssertUtils.AssertHasTagValue(metricPoint, HangfireTagBuilder.TagWorkflowExecutionOutcome, HangfireTagBuilder.OutcomeSuccess);

        // Error type should NOT be present for successful execution
        AssertUtils.AssertHasNoTag(metricPoint, HangfireTagBuilder.TagErrorType);
    }

    [Fact]
    public async Task Should_Record_ExecutionDuration_Metric_With_ErrorType_For_Failed_Job()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert
        var durationMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionDurationMetricName);
        AssertUtils.AssertHasMetricPoints(durationMetric);

        var metricPoints = durationMetric!.ToMetricPointList();
        var metricPoint = metricPoints.First();

        var count = metricPoint.GetHistogramCount();
        Assert.True(count >= 1, $"Expected histogram count >= 1, got {count}");

        // Validate tags for failed execution
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowTaskName);
        AssertUtils.AssertHasTag(metricPoint, HangfireTagBuilder.TagWorkflowPlatformName);
        AssertUtils.AssertHasTagValue(metricPoint, HangfireTagBuilder.TagWorkflowExecutionOutcome, HangfireTagBuilder.OutcomeFailure);

        // Error type is conditionally required for failed execution
        AssertUtils.AssertTagContains(metricPoint, HangfireTagBuilder.TagErrorType, "Exception");
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

        // Assert
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
        AssertUtils.AssertHasTag(metricPoint, "workflow.task.name");
        AssertUtils.AssertHasTag(metricPoint, "workflow.platform.name");
    }

    [Fact]
    public async Task Should_Record_ExecutionStatus_States_For_Successful_Job()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert
        var statusMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionStatusMetricName);
        AssertUtils.AssertHasMetricPoints(statusMetric);
        Assert.Equal(MetricType.LongSumNonMonotonic, statusMetric!.MetricType);

        var metricPoints = statusMetric.ToMetricPointList();

        // Verify all points have required tags
        foreach (var point in metricPoints)
        {
            AssertUtils.AssertHasTag(point, HangfireTagBuilder.TagWorkflowTaskName);
            AssertUtils.AssertHasTag(point, HangfireTagBuilder.TagWorkflowPlatformName);
            AssertUtils.AssertHasTag(point, HangfireTagBuilder.TagWorkflowExecutionState);
            AssertUtils.AssertHasTag(point, HangfireTagBuilder.TagWorkflowTriggerType);
        }

        // Verify state transitions balance out to final state
        var pendingSum = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StatePending)?.GetSumLong() ?? 0;
        var executingSum = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateExecuting)?.GetSumLong() ?? 0;
        var completedSum = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateCompleted)?.GetSumLong() ?? 0;

        Assert.Equal(0, pendingSum);
        Assert.Equal(0, executingSum);
        Assert.Equal(1, completedSum);

        // Verify completed state has correct attributes
        var completedPoint = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateCompleted);
        Assert.NotNull(completedPoint);

        AssertUtils.AssertHasNoTag(completedPoint.Value, HangfireTagBuilder.TagErrorType);
        AssertUtils.AssertHasTagValue(completedPoint.Value, HangfireTagBuilder.TagWorkflowTriggerType, HangfireTagBuilder.TriggerTypeApi);
    }

    [Fact]
    public async Task Should_Record_ExecutionStatus_ErrorType_For_Failed_Job()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert
        var statusMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionStatusMetricName);
        AssertUtils.AssertHasMetricPoints(statusMetric);
        Assert.Equal(MetricType.LongSumNonMonotonic, statusMetric!.MetricType);

        var metricPoints = statusMetric.ToMetricPointList();

        // Verify state transitions balance out to final state (completed with error)
        var pendingSum = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StatePending)?.GetSumLong() ?? 0;
        var executingSum = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateExecuting)?.GetSumLong() ?? 0;
        var completedSum = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateCompleted)?.GetSumLong() ?? 0;

        Assert.Equal(0, pendingSum);
        Assert.Equal(0, executingSum);
        Assert.Equal(1, completedSum);

        // Find the completed state metric point (should have error type for failed job)
        var completedPoint = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateCompleted);
        Assert.NotNull(completedPoint);

        // Verify error type is present and contains exception information
        AssertUtils.AssertTagContains(completedPoint.Value, HangfireTagBuilder.TagErrorType, "Exception");

        // Verify trigger type
        AssertUtils.AssertHasTagValue(completedPoint.Value, HangfireTagBuilder.TagWorkflowTriggerType, HangfireTagBuilder.TriggerTypeApi);
    }

    [Fact]
    public async Task Should_Record_All_Metrics_For_Multiple_Jobs()
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
        var jobId3 = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());

        await this.hangfireFixture.WaitJobProcessedAsync(jobId1, 5);
        await this.hangfireFixture.WaitJobProcessedAsync(jobId2, 5);
        await this.hangfireFixture.WaitJobProcessedAsync(jobId3, 5);

        meterProvider.ForceFlush();

        // Assert - All metric types should be present
        exportedItems.GetMetric(HangfireMetrics.ExecutionCountMetricName);
        exportedItems.GetMetric(HangfireMetrics.ExecutionDurationMetricName);
        exportedItems.GetMetric(HangfireMetrics.ExecutionStatusMetricName);
        exportedItems.GetMetric(HangfireMetrics.QueueLatencyMetricName);

        // Verify execution.count has at least 3 executions
        var executionCountMetric = exportedItems.First(m => m.Name == HangfireMetrics.ExecutionCountMetricName);
        var executionCountPoints = executionCountMetric.ToMetricPointList();

        var totalExecutions = executionCountPoints.Sum(mp => mp.GetSumLong());
        Assert.True(totalExecutions >= 3, $"Expected at least 3 executions, got {totalExecutions}");
    }

    [Fact]
    public async Task Should_Track_Success_And_Failure_Outcomes_Separately()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act - Execute one successful and one failed job
        var successJobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        var failJobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());

        await this.hangfireFixture.WaitJobProcessedAsync(successJobId, 5);
        await this.hangfireFixture.WaitJobProcessedAsync(failJobId, 5);

        meterProvider.ForceFlush();

        // Assert - Check execution duration metric has both outcomes
        var durationMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionDurationMetricName);
        AssertUtils.AssertHasMetricPoints(durationMetric);

        var metricPoints = durationMetric!.ToMetricPointList();

        // Verify success outcome exists without error type
        var successPoint = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionOutcome, HangfireTagBuilder.OutcomeSuccess);
        Assert.NotNull(successPoint);
        AssertUtils.AssertHasNoTag(successPoint.Value, HangfireTagBuilder.TagErrorType);

        // Verify failure outcome exists with error type
        var failurePoint = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionOutcome, HangfireTagBuilder.OutcomeFailure);
        Assert.NotNull(failurePoint);
        AssertUtils.AssertHasTag(failurePoint.Value, HangfireTagBuilder.TagErrorType);
    }

    [Fact]
    public async Task Should_Record_TriggerType_Cron_For_Recurring_Jobs()
    {
        // Arrange
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act - Create a recurring job and trigger it manually
        RecurringJob.AddOrUpdate("test-recurring-job", () => new TestJob().Execute(), Cron.Minutely());
        var jobId = RecurringJob.TriggerJob("test-recurring-job");

        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        meterProvider.ForceFlush();

        // Assert - Check execution status metric has cron trigger type
        var statusMetric = exportedItems.GetMetric(HangfireMetrics.ExecutionStatusMetricName);
        AssertUtils.AssertHasMetricPoints(statusMetric);
        var metricPoints = statusMetric!.ToMetricPointList();

        // Find completed state metric point
        var completedPoint = metricPoints.FindFirstWithTag(HangfireTagBuilder.TagWorkflowExecutionState, HangfireTagBuilder.StateCompleted);

        Assert.NotNull(completedPoint);

        // Verify trigger type is 'cron' for recurring jobs
        AssertUtils.AssertHasTagValue(completedPoint.Value, HangfireTagBuilder.TagWorkflowTriggerType, HangfireTagBuilder.TriggerTypeCron);

        // Cleanup
        RecurringJob.RemoveIfExists("test-recurring-job");
    }

    [Fact]
    public void AddHangfireInstrumentation_Should_Register_Meter()
    {
        // Arrange & Act
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Force a job to generate metrics
        BackgroundJob.Enqueue<TestJob>(x => x.Execute());

        // Assert - The meter should be registered and ready to emit metrics
        // We don't need to wait for job completion, just verify the provider was configured
        Assert.NotNull(meterProvider);
    }
}
