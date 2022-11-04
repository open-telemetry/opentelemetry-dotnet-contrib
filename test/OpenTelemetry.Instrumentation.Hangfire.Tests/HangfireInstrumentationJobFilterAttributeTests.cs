// <copyright file="HangfireInstrumentationJobFilterAttributeTests.cs" company="OpenTelemetry Authors">
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
