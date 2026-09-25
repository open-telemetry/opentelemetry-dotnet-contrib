// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Tests.Trace;

public class AutoFlushActivityProcessorTests
{
    [Fact]
    public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndMatchingSpan_Flush()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer), 5000)
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.True(activityProcessor.ForceFlushCalled);
    }

    [Fact]
    public void AutoFlushActivityProcessor_FlushAfterLocalServerSideRootSpans_EndNonMatchingSpan_DoesNothing()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(a => a.Parent == null && (a.Kind == ActivityKind.Server || a.Kind == ActivityKind.Consumer))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Client);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.False(activityProcessor.ForceFlushCalled);
    }

    [Fact]
    public void AutoFlushActivityProcessor_PredicateThrows_DoesNothing()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(_ => throw new Exception("Predicate throws an exception."))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.False(activityProcessor.ForceFlushCalled);
    }

    [Fact]
    public void AutoFlushActivityProcessor_ForceFlushTimesOut_LogsWarning()
    {
        var sourceName = GetTestMethodName();
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new ForceFlushFailedEventListener(capturedEvents);

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(new FailingForceFlushProcessor())
            .AddAutoFlushActivityProcessor(a => a.Parent == null, 1234)
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        var forceFlushFailedEvent = Assert.Single(capturedEvents, e => e.EventId == 7);
        Assert.Equal(1234, forceFlushFailedEvent.Payload?[0]);
    }

    [Fact]
    public void AutoFlushActivityProcessor_ForceFlushSucceeds_DoesNotLogWarning()
    {
        var activityProcessor = new TestActivityProcessor();
        var sourceName = GetTestMethodName();
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new ForceFlushFailedEventListener(capturedEvents);

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor)
            .AddAutoFlushActivityProcessor(a => a.Parent == null, 1234)
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("name", ActivityKind.Server);
        Assert.NotNull(activity);
        activity.Stop();

        Assert.True(activityProcessor.ForceFlushCalled);
        Assert.DoesNotContain(capturedEvents, e => e.EventId == 7);
    }

    private static string GetTestMethodName([CallerMemberName] string callingMethodName = "")
    {
        return callingMethodName;
    }

    private sealed class FailingForceFlushProcessor : BaseProcessor<Activity>
    {
        protected override bool OnForceFlush(int timeoutMilliseconds) => false;
    }

    private sealed class ForceFlushFailedEventListener : EventListener
    {
        private readonly List<EventWrittenEventArgs> capturedEvents;
        private readonly int creatingThreadId;

        public ForceFlushFailedEventListener(List<EventWrittenEventArgs> capturedEvents)
        {
            this.capturedEvents = capturedEvents;
            this.creatingThreadId = Environment.CurrentManagedThreadId;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "OpenTelemetry-Extensions")
            {
                this.EnableEvents(eventSource, EventLevel.Warning, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (Environment.CurrentManagedThreadId == this.creatingThreadId)
            {
                this.capturedEvents.Add(eventData);
            }
        }
    }
}
