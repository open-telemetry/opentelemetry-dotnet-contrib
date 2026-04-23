// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Hangfire.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

[Collection("Hangfire")]
public class HangfireInstrumentationJobFilterAttributeTests
{
    private readonly HangfireFixture hangfireFixture = new();

    [Fact]
    public async Task Should_Create_Activity()
    {
        // Arrange
        var exportedItems = new List<Activity>();

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation()
                                 .AddInMemoryExporter(exportedItems)
                                 .SetSampler<AlwaysOnSampler>()
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);

        Assert.Contains("JOB TestJob.Execute", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
    }

    [Fact]
    public async Task Should_Create_Activity_With_Status_Error_When_Job_Failed()
    {
        // Arrange
        var exportedItems = new List<Activity>();

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation()
                                 .AddInMemoryExporter(exportedItems)
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);

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

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation(options => options.RecordException = true)
                                 .AddInMemoryExporter(exportedItems)
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);

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

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation(options => options.RecordException = false)
                                 .AddInMemoryExporter(exportedItems)
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.ThrowException());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);

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

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation(options => options.DisplayNameFunc = backgroundJob => $"JOB {backgroundJob.Id}")
                                 .AddInMemoryExporter(exportedItems)
                                 .SetSampler<AlwaysOnSampler>()
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);

        Assert.Contains($"JOB {jobId}", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
    }

    [Fact]
    public async Task Should_Fall_Back_To_Default_DisplayName_When_DisplayNameFunc_Is_Null()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        using var tel = Sdk.CreateTracerProviderBuilder()
            .AddHangfireInstrumentation(options => options.DisplayNameFunc = null!)
            .AddInMemoryExporter(exportedItems)
            .SetSampler<AlwaysOnSampler>()
            .Build();

        // Act
        var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
        await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);
        Assert.Contains("JOB TestJob.Execute", activity.DisplayName);
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

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation(configure)
                                 .AddProcessor(activityProcessor)
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(processedItems);

        Assert.Equal(shouldRecord, activity.IsAllDataRequested);
        Assert.Equal(shouldRecord, activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [Fact]
    public async Task Should_Not_Inject_Invalid_Context()
    {
        // Arrange
        var exportedItems = new List<Activity>();

        using var listener = new OpenTelemetryEventListener();

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation()
                                 .AddInMemoryExporter(exportedItems)
                                 .SetSampler<AlwaysOffSampler>()
                                 .Build())
        {
            // Act
            var jobId = BackgroundJob.Enqueue<TestJob>(x => x.Execute());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        Assert.All(listener.Messages, args => Assert.NotEqual("FailedToInjectActivityContext", args.EventName));
    }

    [Fact]
    public void OnPerforming_Should_Apply_Job_Baggage_And_OnPerformed_Should_Restore_Previous_Baggage()
    {
        using var listener = CreateHangfireActivityListener();
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions());
        var performingContext = CreatePerformingContext(storage, connection);
        var previousBaggage = Baggage.Create(new Dictionary<string, string>
        {
            ["previous-key"] = "previous-value",
        });
        var originalPropagator = Propagators.DefaultTextMapPropagator;

        Baggage.Current = previousBaggage;
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));

        var activityContextData = new Dictionary<string, string>();
        Propagators.DefaultTextMapPropagator.Inject(
             new PropagationContext(
                 new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
                 Baggage.Create(new Dictionary<string, string>
                 {
                     ["job-key"] = "job-value",
                 })),
             activityContextData,
             static (jobParams, key, value) => jobParams[key] = value);
        connection.SetJobParameter(
            performingContext.BackgroundJob.Id,
            HangfireInstrumentationConstants.ActivityContextKey,
            JsonSerializer.Serialize(activityContextData));

        try
        {
            filter.OnPerforming(performingContext);

            Assert.Equal("job-value", Baggage.Current.GetBaggage("job-key"));
            Assert.Null(Baggage.Current.GetBaggage("previous-key"));

            filter.OnPerformed(new PerformedContext(performingContext, null, false, null));

            Assert.Equal("previous-value", Baggage.Current.GetBaggage("previous-key"));
            Assert.Null(Baggage.Current.GetBaggage("job-key"));
        }
        finally
        {
            Baggage.Current = default;
            Sdk.SetDefaultTextMapPropagator(originalPropagator);
        }
    }

    [Fact]
    public void OnPerforming_Without_Propagation_Context_Should_Clear_Baggage_During_The_Job_And_Restore_It_Afterward()
    {
        using var listener = CreateHangfireActivityListener();
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions());
        var performingContext = CreatePerformingContext(storage, connection);
        var previousBaggage = Baggage.Create(new Dictionary<string, string>
        {
            ["previous-key"] = "previous-value",
        });
        Baggage.Current = previousBaggage;

        try
        {
            filter.OnPerforming(performingContext);

            Assert.Equal(0, Baggage.Current.Count);

            filter.OnPerformed(new PerformedContext(performingContext, null, false, null));

            Assert.Equal("previous-value", Baggage.Current.GetBaggage("previous-key"));
        }
        finally
        {
            Baggage.Current = default;
        }
    }

    private static ActivityListener CreateHangfireActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == HangfireInstrumentation.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static PerformingContext CreatePerformingContext(JobStorage storage, IStorageConnection connection)
    {
        var client = new BackgroundJobClient(storage);
        var jobId = client.Create(Job.FromExpression<TestJob>(x => x.Execute()), new EnqueuedState());
        var backgroundJob = new BackgroundJob(jobId, Job.FromExpression<TestJob>(x => x.Execute()), DateTime.UtcNow);
        return new PerformingContext(new PerformContext(storage, connection, backgroundJob, new StubJobCancellationToken()));
    }

    private class OpenTelemetryEventListener : EventListener
    {
        private const string EventSourceName = "OpenTelemetry-Api";

        private readonly Queue<EventWrittenEventArgs> events = new();
        private readonly AutoResetEvent eventWritten = new(false);
        private EventSource? apiEventSource;

        public IEnumerable<EventWrittenEventArgs> Messages
        {
            get
            {
                if (this.events.Count == 0)
                {
                    this.eventWritten.WaitOne(TimeSpan.FromSeconds(3));
                }

                while (this.events.Count != 0)
                {
                    yield return this.events.Dequeue();
                }
            }
        }

        public override void Dispose()
        {
            if (this.apiEventSource != null)
            {
                this.DisableEvents(this.apiEventSource);
            }

            base.Dispose();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == EventSourceName)
            {
                this.apiEventSource = eventSource;
                this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == EventSourceName)
            {
                this.events.Enqueue(eventData);
                this.eventWritten.Set();
            }
        }
    }

    private sealed class StubJobCancellationToken : IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;

        public void ThrowIfCancellationRequested()
        {
        }
    }
}
