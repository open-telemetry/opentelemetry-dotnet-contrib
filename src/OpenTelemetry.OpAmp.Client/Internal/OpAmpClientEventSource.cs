// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.OpAmp.Client.Internal;

[EventSource(Name = "OpenTelemetry-OpAmp-Client")]
internal sealed class OpAmpClientEventSource : EventSource
{
    public static OpAmpClientEventSource Log = new();

    // General events 1-499
    private const int EventIdInvalidWsFrame = 1;
    private const int EventIdTransportCloseFailure = 2;
    private const int EventIdHttpResponseReceived = 3;
    private const int EventIdOversizedWebSocketMessage = 4;
    private const int EventIdFrameProcessingFailure = 5;
    private const int EventIdEffectiveConfigSizeLimitViolation = 6;

    // Service events 500-999
    private const int EventIdHeartbeatServiceStart = 500;
    private const int EventIdHeartbeatServiceStop = 501;
    private const int EventIdHeartbeatServiceTickFailure = 502;
    private const int EventIdHeartbeatServiceTimerUpdateFailure = 503;
    private const int EventIdHeartbeatServiceTimerUpdateReceived = 504;

    // Message queue events 1000-1099
    private const int EventIdQueueingIdentificationMessage = 1_000;
    private const int EventIdQueueingHeartbeatMessage = 1_001;
    private const int EventIdQueueingAgentDisconnectMessage = 1_002;
    private const int EventIdQueueingEffectiveConfigMessage = 1_003;
    private const int EventIdQueueingCustomCapabilitiesMessage = 1_004;
    private const int EventIdQueueingCustomMessageMessage = 1_005;
    private const int EventIdQueueingRemoteConfigStatusMessage = 1_006;
    private const int EventIdQueueingFullStateReportMessage = 1_007;

    // Message send events 1100-1199
    private const int EventIdSendingMessage = 1_100;
    private const int EventIdFailedToSendMessage = 1_101;

    [Event(EventIdInvalidWsFrame, Message = "Received invalid WebSocket frame header: {0}. Dropping the frame.", Level = EventLevel.Warning)]
    public void InvalidWsFrame(string errorMessage)
    {
        this.WriteEvent(EventIdInvalidWsFrame, errorMessage);
    }

    [NonEvent]
    public void TransportCloseException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.TransportCloseFailure(ex.ToInvariantString());
        }
    }

    [Event(EventIdTransportCloseFailure, Message = "WebSocket close failed: {0}", Level = EventLevel.Warning)]
    public void TransportCloseFailure(string exception)
    {
        this.WriteEvent(EventIdTransportCloseFailure, exception);
    }

    [NonEvent]
    public void OversizedWebSocketMessageReceived(int minimumBytes, int limitBytes)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.OversizedWebSocketMessage(minimumBytes, limitBytes);
        }
    }

    [Event(EventIdOversizedWebSocketMessage, Message = "OpAMP server WebSocket message discarded: message is at least {0} bytes, exceeding the {1}-byte limit. The connection will be closed and the frame will not be processed.", Level = EventLevel.Warning)]
    public void OversizedWebSocketMessage(int minimumBytes, int limitBytes)
    {
        this.WriteEvent(EventIdOversizedWebSocketMessage, minimumBytes, limitBytes);
    }

    [NonEvent]
    public void HttpResponseBytesReceived(int bytes)
    {
        if (this.IsEnabled(EventLevel.Verbose, EventKeywords.All))
        {
            this.HttpResponseReceived(bytes);
        }
    }

    [Event(EventIdHttpResponseReceived, Message = "OpAMP HTTP response received: {0} bytes.", Level = EventLevel.Verbose)]
    public void HttpResponseReceived(int bytes)
    {
        this.WriteEvent(EventIdHttpResponseReceived, bytes);
    }

    [NonEvent]
    public void FrameProcessingException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FrameProcessingFailure(ex.ToInvariantString());
        }
    }

    [Event(EventIdFrameProcessingFailure, Message = "Failed to process incoming server frame. The frame was dropped: {0}", Level = EventLevel.Warning)]
    public void FrameProcessingFailure(string exception)
    {
        this.WriteEvent(EventIdFrameProcessingFailure, exception);
    }

    [NonEvent]
    public void EffectiveConfigSizeLimitExceeded(int maxBytes)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.EffectiveConfigSizeLimitViolation(maxBytes);
        }
    }

    [Event(EventIdEffectiveConfigSizeLimitViolation, Message = "Configuration file exceeds maximum allowed size of {0} bytes.", Level = EventLevel.Warning)]
    public void EffectiveConfigSizeLimitViolation(int maxBytes)
    {
        this.WriteEvent(EventIdEffectiveConfigSizeLimitViolation, maxBytes);
    }

    [Event(EventIdHeartbeatServiceStart, Message = "Heartbeat service started.", Level = EventLevel.Informational)]
    public void HeartbeatServiceStart()
    {
        this.WriteEvent(EventIdHeartbeatServiceStart);
    }

    [Event(EventIdHeartbeatServiceStop, Message = "Heartbeat service stopped.", Level = EventLevel.Informational)]
    public void HeartbeatServiceStop()
    {
        this.WriteEvent(EventIdHeartbeatServiceStop);
    }

    [NonEvent]
    public void HeartbeatServiceTickException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.HeartbeatServiceTickFailure(ex.ToInvariantString());
        }
    }

    [Event(EventIdHeartbeatServiceTickFailure, Message = "Heartbeat error: {0}", Level = EventLevel.Error)]
    public void HeartbeatServiceTickFailure(string exception)
    {
        this.WriteEvent(EventIdHeartbeatServiceTickFailure, exception);
    }

    [NonEvent]
    public void HeartbeatServiceTimerUpdateException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.HeartbeatServiceTimerUpdateFailure(ex.ToInvariantString());
        }
    }

    [Event(EventIdHeartbeatServiceTimerUpdateFailure, Message = "Failed to update timer interval: {0}", Level = EventLevel.Error)]
    public void HeartbeatServiceTimerUpdateFailure(string exception)
    {
        this.WriteEvent(EventIdHeartbeatServiceTimerUpdateFailure, exception);
    }

    [Event(EventIdHeartbeatServiceTimerUpdateReceived, Message = "New heartbeat interval received: {0}s", Level = EventLevel.Informational)]
    public void HeartbeatServiceTimerUpdateReceived(ulong seconds)
    {
        this.WriteEvent(EventIdHeartbeatServiceTimerUpdateReceived, seconds);
    }

    [Event(EventIdQueueingIdentificationMessage, Message = "Queueing identification message.", Level = EventLevel.Informational)]
    public void QueueingIdentificationMessage()
    {
        this.WriteEvent(EventIdQueueingIdentificationMessage);
    }

    [Event(EventIdQueueingHeartbeatMessage, Message = "Queueing heartbeat message.", Level = EventLevel.Informational)]
    public void QueueingHeartbeatMessage()
    {
        this.WriteEvent(EventIdQueueingHeartbeatMessage);
    }

    [Event(EventIdQueueingAgentDisconnectMessage, Message = "Queueing agent disconnect message.", Level = EventLevel.Informational)]
    public void QueueingAgentDisconnectMessage()
    {
        this.WriteEvent(EventIdQueueingAgentDisconnectMessage);
    }

    [Event(EventIdQueueingEffectiveConfigMessage, Message = "Queueing effective config message.", Level = EventLevel.Informational)]
    public void QueueingEffectiveConfigMessage()
    {
        this.WriteEvent(EventIdQueueingEffectiveConfigMessage);
    }

    [Event(EventIdQueueingCustomCapabilitiesMessage, Message = "Queueing custom capabilities message.", Level = EventLevel.Informational)]
    public void QueueingCustomCapabilitiesMessage()
    {
        this.WriteEvent(EventIdQueueingCustomCapabilitiesMessage);
    }

    [Event(EventIdQueueingCustomMessageMessage, Message = "Queueing custom message.", Level = EventLevel.Informational)]
    public void QueueingCustomMessageMessage()
    {
        this.WriteEvent(EventIdQueueingCustomMessageMessage);
    }

    [Event(EventIdQueueingRemoteConfigStatusMessage, Message = "Queueing remote config status message.", Level = EventLevel.Informational)]
    public void QueueingRemoteConfigStatusMessage()
    {
        this.WriteEvent(EventIdQueueingRemoteConfigStatusMessage);
    }

    [Event(EventIdQueueingFullStateReportMessage, Message = "Queueing full state report message.", Level = EventLevel.Informational)]
    public void QueueingFullStateReportMessage()
    {
        this.WriteEvent(EventIdQueueingFullStateReportMessage);
    }

    [Event(EventIdSendingMessage, Message = "Sending OpAMP message.", Level = EventLevel.Informational)]
    public void SendingMessage()
    {
        this.WriteEvent(EventIdSendingMessage);
    }

    [NonEvent]
    public void SendMessageException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedToSendMessage(ex.ToInvariantString());
        }
    }

    [Event(EventIdFailedToSendMessage, Message = "Failed to send OpAMP message: {0}", Level = EventLevel.Error)]
    public void FailedToSendMessage(string exception)
    {
        this.WriteEvent(EventIdFailedToSendMessage, exception);
    }
}
