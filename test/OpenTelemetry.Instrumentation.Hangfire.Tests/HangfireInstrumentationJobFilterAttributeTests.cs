// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Storage.Monitoring;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class HangfireInstrumentationJobFilterAttributeTests : IClassFixture<HangfireFixture>
{
    private HangfireFixture hangfireFixture;

    public HangfireInstrumentationJobFilterAttributeTests(HangfireFixture hangfireFixture)
    {
        this.hangfireFixture = hangfireFixture;
    }

    [Fact]
    public async Task Should_Create_Activity()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.WaitJobProcessedAsync(jobId, 5);

        // Assert
        Assert.Single(exportedItems, i => i.GetTagItem("job.id") as string == jobId);
        var activity = exportedItems.Single(i => i.GetTagItem("job.id") as string == jobId);
        Assert.Contains("JOB TestJob.Execute", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
    }

    [Fact]
    public async Task Should_Create_Activity_With_Status_Error_When_Job_Failed()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        await this.WaitJobProcessedAsync(jobId, 5);

        // Assert
        Assert.Single(exportedItems, i => i.GetTagItem("job.id") as string == jobId);
        var activity = exportedItems.Single(i => i.GetTagItem("job.id") as string == jobId);
        Assert.Contains("JOB TestJob.ThrowException", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains("An exception occurred during performance of the job.", activity.StatusDescription);
        Assert.Empty(activity.Events);
    }

    [Fact]
    public async Task Should_Create_Activity_With_Exception_Event_When_Job_Failed_And_Record_Exception_Is_True()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation(options => options.RecordException = true)
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        await this.WaitJobProcessedAsync(jobId, 5);

        // Assert
        Assert.Single(exportedItems, i => i.GetTagItem("job.id") as string == jobId);
        var activity = exportedItems.Single(i => i.GetTagItem("job.id") as string == jobId);
        Assert.Contains("JOB TestJob.ThrowException", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains("An exception occurred during performance of the job.", activity.StatusDescription);
        Assert.Single(activity.Events, evt => evt.Name == "exception");
    }

    [Fact]
    public async Task Should_Create_Activity_Without_Exception_Event_When_Job_Failed_And_Record_Exception_Is_False()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation(options => options.RecordException = false)
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
        await this.WaitJobProcessedAsync(jobId, 5);

        // Assert
        Assert.Single(exportedItems, i => i.GetTagItem("job.id") as string == jobId);
        var activity = exportedItems.Single(i => i.GetTagItem("job.id") as string == jobId);
        Assert.Contains("JOB TestJob.ThrowException", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains("An exception occurred during performance of the job.", activity.StatusDescription);
        Assert.Empty(activity.Events);
    }

    [Fact]
    public async Task Should_Create_Activity_With_Custom_DisplayName()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation(options => options.DisplayNameFunc = backgroundJob => $"JOB {backgroundJob.Id}")
            .AddInMemoryExporter(exportedItems)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.WaitJobProcessedAsync(jobId, 5);

        // Assert
        Assert.Single(exportedItems, i => i.GetTagItem("job.id") as string == jobId);
        var activity = exportedItems.Single(i => i.GetTagItem("job.id") as string == jobId);
        Assert.Contains($"JOB {jobId}", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
    }

    [Theory]
    [InlineData("null", true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("throw", false)]
    public async Task Should_Respect_Filter_Option(string filter, bool shouldRecord)
    {
        // Arrange
        Action<HangfireInstrumentationOptions> configure = filter switch
        {
            "null" => options => options.Filter = null,
            "true" => options => options.Filter = _ => true,
            "false" => options => options.Filter = _ => false,
            "throw" => options => options.Filter = _ => throw new Exception("Filter throws exception"),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unexpected value"),
        };

        var processedItems = new List<Activity>();
        var activityProcessor = new ProcessorMock<Activity>(onStart: processedItems.Add);

        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation(configure)
            .AddProcessor(activityProcessor)
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.WaitJobProcessedAsync(jobId, 5);

        // Assert
        Assert.Single(processedItems);
        var activity = processedItems.First();

        Assert.Equal(shouldRecord, activity.IsAllDataRequested);
        Assert.Equal(shouldRecord, activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    private async Task WaitJobProcessedAsync(string jobId, int timeToWaitInSeconds)
    {
        var timeout = DateTime.Now.AddSeconds(timeToWaitInSeconds);
        string[] states = new[] { "Enqueued", "Processing" };
        JobDetailsDto jobDetails;
        while (((jobDetails = this.hangfireFixture.MonitoringApi.JobDetails(jobId)) == null || jobDetails.History.All(h => states.Contains(h.StateName)))
               && DateTime.Now < timeout)
        {
            await Task.Delay(500);
        }
    }
}
