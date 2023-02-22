// <copyright file="QuartzDiagnosticListenerTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Trace;
using Quartz;
using Xunit;

namespace OpenTelemetry.Instrumentation.Quartz.Tests;

public class QuartzDiagnosticListenerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(125);

    public QuartzDiagnosticListenerTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    }

    [Fact]
    public async Task Should_Create_Activity()
    {
        // Arrange
        Barrier barrier = new Barrier(2);
        List<DateTime> jobExecTimestamps = new List<DateTime>();

        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddQuartzInstrumentation()
            .AddProcessor(activityProcessor.Object)
            .Build();
        var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
        schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
        var scheduler = await schedulerConfig.BuildScheduler();

        scheduler.Context.Put("BARRIER", barrier);
        scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
        await scheduler.Start();

        JobDataMap jobDataMap = new JobDataMap { { "A", "B" } };

        var name = Guid.NewGuid().ToString();
        var job = JobBuilder.Create<TestJob>()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .StartNow()
            .Build();

        // Act
        await scheduler.ScheduleJob(job, trigger);

        barrier.SignalAndWait(TestTimeout);

        await scheduler.Shutdown(true);

        // Assert
        Assert.Equal(3, activityProcessor.Invocations.Count);
        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        Assert.Contains("execute ", activity.DisplayName);
        Assert.Equal("Quartz.Job.Execute", activity.OperationName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal("Scheduler", activity.Tags.SingleOrDefault(t => t.Key.Equals("scheduler.name")).Value);
        Assert.Equal(SchedulerConstants.DefaultGroup, activity.Tags.SingleOrDefault(t => t.Key.Equals("job.group")).Value);
        Assert.Equal(SchedulerConstants.DefaultGroup, activity.Tags.SingleOrDefault(t => t.Key.Equals("trigger.group")).Value);
    }

    [Fact]
    public async Task Should_Create_Activity_And_Enrich_When_Enrich()
    {
        // Arrange
        Barrier barrier = new Barrier(2);
        List<DateTime> jobExecTimestamps = new List<DateTime>();

        var activityProcessor = new Mock<BaseProcessor<Activity>>();

        using var tel = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddQuartzInstrumentation(q =>
                q.Enrich = (a, _, payload) =>
                {
                    if (payload is IJobDetail jobDetail)
                    {
                        var dataMap = jobDetail.JobDataMap;
                        if (dataMap.TryGetValue("TestId", out var value))
                        {
                            a.SetTag("test.id", value);
                        }
                    }
                })
            .AddProcessor(activityProcessor.Object)
            .Build();

        var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
        schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
        var scheduler = await schedulerConfig.BuildScheduler();

        scheduler.Context.Put("BARRIER", barrier);
        scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
        await scheduler.Start();

        var testId = Guid.NewGuid().ToString();
        JobDataMap jobDataMap = new JobDataMap { { "TestId", testId } };

        var name = Guid.NewGuid().ToString();
        var job = JobBuilder.Create<TestJob>()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .StartNow()
            .Build();

        // Act
        await scheduler.ScheduleJob(job, trigger);

        barrier.SignalAndWait(TestTimeout);

        await scheduler.Shutdown(true);

        // Assert
        Assert.Equal(3, activityProcessor.Invocations.Count);
        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        Assert.Equal("Quartz.Job.Execute", activity.OperationName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal("Scheduler", activity.Tags.SingleOrDefault(t => t.Key.Equals("scheduler.name")).Value);
        Assert.Equal(SchedulerConstants.DefaultGroup, activity.Tags.SingleOrDefault(t => t.Key.Equals("job.group")).Value);
        Assert.Equal(SchedulerConstants.DefaultGroup, activity.Tags.SingleOrDefault(t => t.Key.Equals("trigger.group")).Value);
        Assert.Equal(testId, activity.Tags.SingleOrDefault(t => t.Key.Equals("test.id")).Value);
    }

    [Fact]
    public async Task Should_Record_Exception_When_Record_Exception_Enabled()
    {
        // Arrange
        Barrier barrier = new Barrier(2);
        List<DateTime> jobExecTimestamps = new List<DateTime>();

        var activityProcessor = new Mock<BaseProcessor<Activity>>();

        using var tel = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddQuartzInstrumentation(q =>
                q.RecordException = true)
            .AddProcessor(activityProcessor.Object)
            .Build();

        var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
        schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
        var scheduler = await schedulerConfig.BuildScheduler();

        scheduler.Context.Put("BARRIER", barrier);
        scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
        await scheduler.Start();

        var testId = Guid.NewGuid().ToString();
        JobDataMap jobDataMap = new JobDataMap { { "TestId", testId } };

        var name = Guid.NewGuid().ToString();
        var job = JobBuilder.Create<TestJobExecutionExceptionJob>()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .StartNow()
            .Build();

        // Act
        await scheduler.ScheduleJob(job, trigger);

        barrier.SignalAndWait(TimeSpan.FromSeconds(1));

        await scheduler.Shutdown(true);

        // Assert
        Assert.Equal(3, activityProcessor.Invocations.Count);
        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        Assert.Equal("exception", activity.Events.First().Name);
        Assert.Equal("ERROR", activity.Tags.SingleOrDefault(t => t.Key.Equals(SpanAttributeConstants.StatusCodeKey)).Value);
        Assert.Equal("Catch me if you can!", activity.Tags.SingleOrDefault(t => t.Key.Equals(SpanAttributeConstants.StatusDescriptionKey)).Value);
    }

    [Fact]
    public async Task Should_Enrich_Exception_When_Record_Exception_Enabled_And_Enrich()
    {
        // Arrange
        Barrier barrier = new Barrier(2);
        List<DateTime> jobExecTimestamps = new List<DateTime>();

        var activityProcessor = new Mock<BaseProcessor<Activity>>();

        using var tel = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddQuartzInstrumentation(q =>
            {
                q.RecordException = true;
                q.Enrich = (a, _, p) =>
                {
                    if (p is IJobDetail jobDetail)
                    {
                        var dataMap = jobDetail.JobDataMap;
                        if (dataMap.TryGetValue("TestId", out var value))
                        {
                            a.SetTag("test.id", value);
                        }
                    }
                };
            })
            .AddProcessor(activityProcessor.Object)
            .Build();

        var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
        schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
        var scheduler = await schedulerConfig.BuildScheduler();

        scheduler.Context.Put("BARRIER", barrier);
        scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
        await scheduler.Start();

        var testId = Guid.NewGuid().ToString();
        JobDataMap jobDataMap = new JobDataMap { { "TestId", testId } };

        var name = Guid.NewGuid().ToString();
        var job = JobBuilder.Create<TestJobExecutionExceptionJob>()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .StartNow()
            .Build();

        // Act
        await scheduler.ScheduleJob(job, trigger);

        barrier.SignalAndWait(TimeSpan.FromSeconds(1));

        await scheduler.Shutdown(true);

        // Assert
        Assert.Equal(3, activityProcessor.Invocations.Count);
        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

        Assert.Equal("ERROR", activity.Tags.SingleOrDefault(t => t.Key.Equals(SpanAttributeConstants.StatusCodeKey)).Value);
        Assert.Equal("Catch me if you can!", activity.Tags.SingleOrDefault(t => t.Key.Equals(SpanAttributeConstants.StatusDescriptionKey)).Value);
        Assert.Equal(testId, activity.Tags.SingleOrDefault(t => t.Key.Equals("test.id")).Value);
    }

    [Fact]
    public async Task Should_Creates_Activity_Event_On_Job_Execution_Exception()
    {
        // Arrange
        Barrier barrier = new Barrier(2);
        List<DateTime> jobExecTimestamps = new List<DateTime>();

        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddQuartzInstrumentation(q =>
            {
                q.RecordException = true;
                q.Enrich = (_, s, _) =>
                {
                    if (s.Equals("OnException"))
                    {
                        throw new Exception("Enrich Exception");
                    }
                };
            })
            .AddProcessor(activityProcessor.Object)
            .Build();

        var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
        schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
        var scheduler = await schedulerConfig.BuildScheduler();

        scheduler.Context.Put("BARRIER", barrier);
        scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
        await scheduler.Start();

        var testId = Guid.NewGuid().ToString();
        JobDataMap jobDataMap = new JobDataMap { { "TestId", testId } };

        var name = Guid.NewGuid().ToString();
        var job = JobBuilder.Create<TestJobExecutionExceptionJob>()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .StartNow()
            .Build();

        // Act
        await scheduler.ScheduleJob(job, trigger);

        barrier.SignalAndWait(TimeSpan.FromSeconds(1));

        await scheduler.Shutdown(true);

        // Assert
        Assert.Equal(3, activityProcessor.Invocations.Count);
        var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];
        Assert.Equal("exception", activity.Events.First().Name);
        Assert.Equal("Quartz.JobExecutionException", activity.Events.First().Tags.SingleOrDefault(t => t.Key.Equals(SemanticConventions.AttributeExceptionType)).Value);
        Assert.Equal("Catch me if you can!", activity.Events.First().Tags.SingleOrDefault(t => t.Key.Equals(SemanticConventions.AttributeExceptionMessage)).Value);
    }

    [Fact]
    public async Task Should_Not_Record_Activity_When_Trace_Operation_Is_Not_Present()
    {
        // Arrange
        Barrier barrier = new Barrier(2);
        List<DateTime> jobExecTimestamps = new List<DateTime>();

        var activityProcessor = new Mock<BaseProcessor<Activity>>();

        using var tel = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddQuartzInstrumentation(q =>
            {
                q.TracedOperations = new HashSet<string>();
            })
            .AddProcessor(activityProcessor.Object)
            .Build();

        var schedulerConfig = SchedulerBuilder.Create("AUTO", "Scheduler");
        schedulerConfig.UseDefaultThreadPool(x => x.MaxConcurrency = 10);
        var scheduler = await schedulerConfig.BuildScheduler();

        scheduler.Context.Put("BARRIER", barrier);
        scheduler.Context.Put("DATESTAMPS", jobExecTimestamps);
        await scheduler.Start();

        var testId = Guid.NewGuid().ToString();
        JobDataMap jobDataMap = new JobDataMap { { "TestId", testId } };

        var name = Guid.NewGuid().ToString();
        var job = JobBuilder.Create<TestJob>()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity(name, SchedulerConstants.DefaultGroup)
            .StartNow()
            .Build();

        // Act
        await scheduler.ScheduleJob(job, trigger);

        barrier.SignalAndWait(TestTimeout);

        await scheduler.Shutdown(true);

        // Assert
        var activities = activityProcessor.Invocations.SelectMany(i => i.Arguments.OfType<Activity>())
            .Where(a => a.IsAllDataRequested);
        Assert.Empty(activities);
    }
}
