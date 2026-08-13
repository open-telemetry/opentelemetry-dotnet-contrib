// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Xml.Linq;
using OpenTelemetry.Exporter.Geneva.Transports;

namespace OpenTelemetry.Exporter.Geneva.Tests.Internal.Transports;

public class EtwDataTransportTests
{
    // The agent parses the raw ETW user-data blob as the forward protocol buffer. .NET prepends a synthetic 4-byte
    // length to every field declared in the manifest, so the event must declare none. Declaring a single byte[] field
    // shifted the whole payload by 4 bytes and the agent rejected it with "Bad forward protocol format".
    [Fact]
    public void TraceEventManifestDeclaresNoPayloadTemplate()
    {
        var manifest = EventSource.GenerateManifest(typeof(EtwDataTransport.EtwEventSource), null);

        Assert.NotNull(manifest);

        var eventId = ((int)EtwDataTransport.EtwEventSource.EtwEventId.TraceEvent).ToString(CultureInfo.InvariantCulture);
        var traceEvent = Assert.Single(
            XDocument.Parse(manifest).Descendants(),
            e => e.Name.LocalName == "event" && e.Attribute("value")?.Value == eventId);

        Assert.Null(traceEvent.Attribute("template"));
    }

    [Fact]
    public void SendEventWritesRawPayloadWithoutDeclaredFields()
    {
        var randomProviderName = "x" + Guid.NewGuid().ToString("N");
        using var listener = new TestEventSourceListener(randomProviderName);
        using var transport = new EtwDataTransport(randomProviderName);
        byte[] payload = [1, 2, 3, 4];

        transport.Send(payload, payload.Length);

        var @event = Assert.Single(
            listener.Events,
            e => e.EventId == (int)EtwDataTransport.EtwEventSource.EtwEventId.TraceEvent);

        var payloadFieldCount = @event.Payload?.Count ?? 0;
        Assert.Equal(0, payloadFieldCount);
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
        /// <para/>
        /// Concurrent because <see cref="OnEventWritten"/> may be called from any thread, including while a test
        /// enumerates this collection.
        /// </summary>
        public ConcurrentQueue<EventWrittenEventArgs> Events { get; } = new();

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
        protected override void OnEventWritten(EventWrittenEventArgs eventData) => this.Events.Enqueue(eventData);
    }
}
