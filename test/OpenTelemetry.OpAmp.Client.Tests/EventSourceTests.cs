// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpAmpClientEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<OpAmpClientEventSource>();

    [Fact]
    public void OpAmpClientEventSource_LogsMessageEvents()
    {
        using var listener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        var failureMessage = $"send failed {Guid.NewGuid():N}";
        var queueLogActions = new (Action LogQueueingMessage, string EventName)[]
        {
            (OpAmpClientEventSource.Log.QueueingIdentificationMessage, nameof(OpAmpClientEventSource.QueueingIdentificationMessage)),
            (OpAmpClientEventSource.Log.QueueingHeartbeatMessage, nameof(OpAmpClientEventSource.QueueingHeartbeatMessage)),
            (OpAmpClientEventSource.Log.QueueingAgentDisconnectMessage, nameof(OpAmpClientEventSource.QueueingAgentDisconnectMessage)),
            (OpAmpClientEventSource.Log.QueueingEffectiveConfigMessage, nameof(OpAmpClientEventSource.QueueingEffectiveConfigMessage)),
            (OpAmpClientEventSource.Log.QueueingCustomCapabilitiesMessage, nameof(OpAmpClientEventSource.QueueingCustomCapabilitiesMessage)),
            (OpAmpClientEventSource.Log.QueueingCustomMessageMessage, nameof(OpAmpClientEventSource.QueueingCustomMessageMessage)),
            (OpAmpClientEventSource.Log.QueueingRemoteConfigStatusMessage, nameof(OpAmpClientEventSource.QueueingRemoteConfigStatusMessage)),
            (OpAmpClientEventSource.Log.QueueingFullStateReportMessage, nameof(OpAmpClientEventSource.QueueingFullStateReportMessage)),
        };

        foreach (var logAction in queueLogActions)
        {
            logAction.LogQueueingMessage();

            Assert.Contains(listener.Events, e => e.EventName == logAction.EventName);
        }

        OpAmpClientEventSource.Log.SendingMessage();
        OpAmpClientEventSource.Log.SendMessageException(new InvalidOperationException(failureMessage));

        Assert.Contains(listener.Events, e => e.EventName == nameof(OpAmpClientEventSource.SendingMessage));
        Assert.Contains(
            listener.Events,
            e => e.EventName == nameof(OpAmpClientEventSource.FailedToSendMessage)
                && e.Payload![0] is string exception
                && exception.Contains(failureMessage));
    }
}
