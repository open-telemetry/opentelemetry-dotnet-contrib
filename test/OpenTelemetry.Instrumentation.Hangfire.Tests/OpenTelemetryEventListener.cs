// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

internal class OpenTelemetryEventListener : EventListener
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
