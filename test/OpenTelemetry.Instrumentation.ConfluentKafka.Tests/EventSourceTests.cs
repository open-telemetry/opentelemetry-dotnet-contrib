// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_ConfluentKafkaInstrumentationEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<ConfluentKafkaInstrumentationEventSource>();

    [Fact]
    public void FailedToFetchClusterId_LogsExceptionDetails()
    {
        var capturedEvents = new List<EventWrittenEventArgs>();

        using var listener = new ClusterIdEventListener(capturedEvents);

        ConfluentKafkaInstrumentationEventSource.Log.FailedToFetchClusterId(new InvalidOperationException("boom"));

        var loggedEvent = Assert.Single(capturedEvents, e => e.EventId == 1);
        Assert.Contains("boom", (string?)loggedEvent.Payload?[0]);
    }

    private sealed class ClusterIdEventListener : EventListener
    {
        private readonly List<EventWrittenEventArgs> capturedEvents;
        private readonly int creatingThreadId;

        public ClusterIdEventListener(List<EventWrittenEventArgs> capturedEvents)
        {
            this.capturedEvents = capturedEvents;
            this.creatingThreadId = Environment.CurrentManagedThreadId;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "OpenTelemetry-Instrumentation-ConfluentKafka")
            {
                this.EnableEvents(eventSource, EventLevel.Warning, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // The ConfluentKafka EventSource is a process-wide singleton, so this listener also
            // receives events written by other test classes that run in parallel. EventListener
            // callbacks run synchronously on the thread that wrote the event, so capturing only
            // events written on the thread that created this listener keeps each test isolated.
            if (Environment.CurrentManagedThreadId == this.creatingThreadId)
            {
                this.capturedEvents.Add(eventData);
            }
        }
    }
}
