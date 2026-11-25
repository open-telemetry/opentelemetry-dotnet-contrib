// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
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
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

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
        Assert.Null(activity.StatusDescription);
        Assert.Empty(activity.Events);
    }

    [Fact]
    public async Task Should_Not_Set_Status_Description_When_Job_Scope_Disposal_Failed()
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
            jobId = BackgroundJob.Enqueue<TestJobWithDisposeException>(x => x.Execute());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var activity = Assert.Single(exportedItems, i => (i.GetTagItem("job.id") as string) == jobId);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
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
        Assert.Null(activity.StatusDescription);

        var exceptionEvent = Assert.Single(activity.Events, evt => evt.Name == "exception");
        Assert.Contains(
            exceptionEvent.Tags,
            tag => tag.Key == "exception.message" && (tag.Value as string) == "An exception occurred during performance of the job.");
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
        Assert.Null(activity.StatusDescription);
        Assert.Empty(activity.Events);
    }

    [Fact]
    public async Task Should_Create_Activity_With_Custom_DisplayName()
    {
        // Arrange
        var exportedItems = new List<Activity>();
        var currentActivityInDisplayNameFunc = new ConcurrentDictionary<string, Activity?>();

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation(options => options.DisplayNameFunc = backgroundJob =>
                                 {
                                     currentActivityInDisplayNameFunc[backgroundJob.Id] = Activity.Current;
                                     return $"JOB {backgroundJob.Id}";
                                 })
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
        Assert.Same(activity, currentActivityInDisplayNameFunc[jobId]);
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
        if (shouldRecord)
        {
            var activity = Assert.Single(processedItems);

            Assert.True(activity.IsAllDataRequested);
            Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
        }
        else
        {
            Assert.Empty(processedItems);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_Not_Export_Activities_Started_By_A_Filtered_Job(bool filtered)
    {
        // Arrange
        var exportedItems = new List<Activity>();

        string jobId;

        using (var provider = Sdk.CreateTracerProviderBuilder()
                                 .AddHangfireInstrumentation(options => options.Filter = _ => !filtered)
                                 .AddSource(TestJob.ActivitySource.Name)
                                 .AddInMemoryExporter(exportedItems)
                                 .Build())
        {
            // Act
            jobId = BackgroundJob.Enqueue<TestJob>(x => x.ExecuteWithChildActivity());
            await this.hangfireFixture.WaitJobProcessedAsync(jobId, 5);

            provider.ForceFlush();
        }

        // Assert
        var childActivities = exportedItems.Where(a => a.Source.Name == TestJob.ActivitySource.Name).ToArray();

        if (filtered)
        {
            Assert.Empty(childActivities);
            Assert.DoesNotContain(exportedItems, a => a.Source.Name == HangfireInstrumentation.ActivitySourceName);
        }
        else
        {
            var jobActivity = Assert.Single(exportedItems, a => (a.GetTagItem("job.id") as string) == jobId);
            var childActivity = Assert.Single(childActivities);
            Assert.Equal(jobActivity.SpanId, childActivity.ParentSpanId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Filtered_Job_Activity_Should_Stay_Current_And_Unrecorded_Until_OnPerformed(bool throwInFilter)
    {
        var startedActivities = 0;
        var stoppedActivities = 0;
        string? idSeenOnStart = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.Source.Name == HangfireInstrumentation.ActivitySourceName)
                {
                    startedActivities++;
                    idSeenOnStart = activity.Id;
                }
            },
            ActivityStopped = activity =>
            {
                if (activity.Source.Name == HangfireInstrumentation.ActivitySourceName)
                {
                    stoppedActivities++;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var parentSource = new ActivitySource(nameof(HangfireInstrumentationJobFilterAttributeTests));
        using var parentActivity = parentSource.StartActivity("parent");
        Assert.NotNull(parentActivity);

        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions
        {
            Filter = throwInFilter ? _ => throw new InvalidOperationException("Filter throws exception") : _ => false,
        });
        var performingContext = CreatePerformingContext(storage, connection);

        filter.OnPerforming(performingContext);

        var jobActivity = Activity.Current;
        Assert.NotNull(jobActivity);
        Assert.Equal(HangfireInstrumentation.ActivitySourceName, jobActivity.Source.Name);
        Assert.Equal(parentActivity.SpanId, jobActivity.ParentSpanId);
        Assert.False(jobActivity.IsAllDataRequested);
        Assert.False(jobActivity.Recorded);
        Assert.Equal(1, startedActivities);
        Assert.Equal(0, stoppedActivities);
        Assert.NotNull(idSeenOnStart);
        Assert.Equal(jobActivity.Id, idSeenOnStart);
        Assert.False(ActivityContext.Parse(idSeenOnStart, null).TraceFlags.HasFlag(ActivityTraceFlags.Recorded));

        filter.OnPerformed(new PerformedContext(performingContext, null, false, null));

        Assert.True(jobActivity.IsStopped);
        Assert.Same(parentActivity, Activity.Current);
        Assert.Equal(1, stoppedActivities);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnPerformed_Should_Record_Exception_Only_For_Recorded_Job_And_Always_Stop_The_Activity(bool filtered)
    {
        var recordedExceptions = 0;

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ExceptionRecorder = (Activity activity, Exception exception, ref TagList tags) =>
            {
                if (activity.Source.Name == HangfireInstrumentation.ActivitySourceName)
                {
                    recordedExceptions++;
                    throw new InvalidOperationException("ExceptionRecorder throws exception");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var parentSource = new ActivitySource(nameof(HangfireInstrumentationJobFilterAttributeTests));
        using var parentActivity = parentSource.StartActivity("parent");
        Assert.NotNull(parentActivity);

        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions
        {
            RecordException = true,
            Filter = _ => !filtered,
        });
        var performingContext = CreatePerformingContext(storage, connection);

        filter.OnPerforming(performingContext);

        var jobActivity = Activity.Current;
        Assert.NotNull(jobActivity);

        using var eventListener = new InMemoryEventListener(HangfireInstrumentationEventSource.Log, EventLevel.Warning);

        filter.OnPerformed(new PerformedContext(performingContext, null, false, new InvalidOperationException("Job failed")));

        var recorderFailures = eventListener.Events
            .Where(e => e.EventId == 3 && ((string?)e.Payload?[0])?.Contains("ExceptionRecorder throws exception") == true);

        if (filtered)
        {
            Assert.Equal(0, recordedExceptions);
            Assert.Equal(ActivityStatusCode.Unset, jobActivity.Status);
            Assert.Empty(recorderFailures);
        }
        else
        {
            Assert.Equal(1, recordedExceptions);
            Assert.Equal(ActivityStatusCode.Error, jobActivity.Status);
            Assert.Single(recorderFailures);
        }

        Assert.True(jobActivity.IsStopped);
        Assert.Same(parentActivity, Activity.Current);
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

    [Fact]
    public void OnPerforming_Should_Not_Share_Baggage_With_Work_Left_Over_From_A_Previous_Job()
    {
        using var listener = CreateHangfireActivityListener();
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions());
        var originalPropagator = Propagators.DefaultTextMapPropagator;
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([new TraceContextPropagator(), new BaggagePropagator()]));

        try
        {
            var firstJob = CreatePerformingContext(storage, connection);
            SetJobBaggage(connection, firstJob, "first-value");
            filter.OnPerforming(firstJob);

            // Work started by the first job that is still running when the next job is performed
            var leftOverContext = ExecutionContext.Capture()!;
            filter.OnPerformed(new PerformedContext(firstJob, null, false, null));

            var secondJob = CreatePerformingContext(storage, connection);
            SetJobBaggage(connection, secondJob, "second-value");
            filter.OnPerforming(secondJob);

            string? leftOverBaggage = null;
            ExecutionContext.Run(leftOverContext, _ => leftOverBaggage = Baggage.Current.GetBaggage("job-key"), null);

            Assert.Equal("second-value", Baggage.Current.GetBaggage("job-key"));
            Assert.Equal("first-value", leftOverBaggage);

            filter.OnPerformed(new PerformedContext(secondJob, null, false, null));
        }
        finally
        {
            Baggage.Current = default;
            Sdk.SetDefaultTextMapPropagator(originalPropagator);
        }

        static void SetJobBaggage(IStorageConnection connection, PerformingContext performingContext, string baggageValue)
        {
            var activityContextData = new Dictionary<string, string>();
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(
                    new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
                    Baggage.Create(new Dictionary<string, string>
                    {
                        ["job-key"] = baggageValue,
                    })),
                activityContextData,
                static (jobParams, key, value) => jobParams[key] = value);
            connection.SetJobParameter(
                performingContext.BackgroundJob.Id,
                HangfireInstrumentationConstants.ActivityContextKey,
                JsonSerializer.Serialize(activityContextData));
        }
    }

    [Fact]
    public void OnPerforming_Should_Not_Expose_Job_Baggage_To_Context_Captured_Before_The_Job()
    {
        using var listener = CreateHangfireActivityListener();
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions());
        var performingContext = CreatePerformingContext(storage, connection);
        var originalPropagator = Propagators.DefaultTextMapPropagator;

        Baggage.Current = Baggage.Create(new Dictionary<string, string>
        {
            ["previous-key"] = "previous-value",
        });
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
            // Work that captured the worker's context before the job started
            var earlierContext = ExecutionContext.Capture()!;

            filter.OnPerforming(performingContext);

            string? previousValue = null;
            string? jobValue = null;
            ExecutionContext.Run(
                earlierContext,
                _ =>
                {
                    previousValue = Baggage.Current.GetBaggage("previous-key");
                    jobValue = Baggage.Current.GetBaggage("job-key");
                },
                null);

            Assert.Equal("previous-value", previousValue);
            Assert.Null(jobValue);

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
    public void OnPerforming_Should_Not_Change_Baggage_When_Reading_Job_Parameters_Fails()
    {
        using var listener = CreateHangfireActivityListener();
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();

        var filter = new HangfireInstrumentationJobFilterAttribute(new HangfireInstrumentationOptions());
        var performingContext = CreatePerformingContext(storage, connection);
        Baggage.Current = Baggage.Create(new Dictionary<string, string>
        {
            ["previous-key"] = "previous-value",
        });

        connection.SetJobParameter(
            performingContext.BackgroundJob.Id,
            HangfireInstrumentationConstants.ActivityContextKey,
            "{ not valid json");

        try
        {
            Assert.ThrowsAny<Exception>(() => filter.OnPerforming(performingContext));

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
