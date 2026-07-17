// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.Geneva.Transports;

namespace OpenTelemetry.Exporter.Geneva.Tests.Internal.Transports;

[Collection(EtwCollection.Name)]
public class EtwDataTransportTests
{
    [Fact]
    public void TestEtwMessageRoundtrip()
    {
        var randomProviderName = "x" + Guid.NewGuid().ToString("N");
        using var listener = new TestEventSourceListener(randomProviderName);
        using var transport = new EtwDataTransport(randomProviderName);
        byte[] payload = [1, 2, 3, 4];

        transport.Send(payload, payload.Length);

        var @event = Assert.Single(listener.Events);

        Assert.NotNull(@event.Payload);
        var eventPayload = Assert.Single(@event.Payload);

        var rawPayload = eventPayload as byte[];
        Assert.NotNull(rawPayload);
        Assert.Equal(payload, rawPayload);
    }

    /// <summary>
    /// Test event source listener.
    /// </summary>
    private sealed class TestEventSourceListener : EventListener
    {
        /// <summary>
        /// Event source name to automatically attach to.
        /// </summary>
        private readonly string eventSourceName;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestEventSourceListener"/> class.
        /// </summary>
        /// <param name="eventSourceName">The name of the event source to attach to.</param>
        public TestEventSourceListener(string eventSourceName)
        {
            this.eventSourceName = eventSourceName;
        }

        /// <summary>
        /// Gets events emitted by ETW.
        /// </summary>
        public List<EventWrittenEventArgs> Events { get; } = [];

        /// <inheritdoc />
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);
            if (string.Equals(eventSource.Name, this.eventSourceName, StringComparison.Ordinal))
            {
                this.EnableEvents(eventSource, EventLevel.LogAlways);
            }
        }

        /// <inheritdoc/>
        protected override void OnEventWritten(EventWrittenEventArgs eventData) => this.Events.Add(eventData);
    }
}
