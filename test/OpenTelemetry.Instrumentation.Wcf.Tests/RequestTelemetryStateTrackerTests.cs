// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.ServiceModel.Channels;
using System.Xml;
using OpenTelemetry.Instrumentation.Wcf.Implementation;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

[Collection("WCF")]
public class RequestTelemetryStateTrackerTests
{
    [Fact]
    public void RemoveTelemetryStatePreventsLateReplyFromCompletingItAgain()
    {
        var messageId = new UniqueId(Guid.NewGuid());
        using var request = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "urn:request");
        request.Headers.MessageId = messageId;
        var state = new RequestTelemetryState();

        Assert.True(RequestTelemetryStateTracker.PushTelemetryState(request, state, TimeSpan.FromHours(1), _ => { }));

        RequestTelemetryStateTracker.RemoveTelemetryState(state);

        using var reply = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "urn:reply");
        reply.Headers.RelatesTo = messageId;
        Assert.Null(RequestTelemetryStateTracker.PopTelemetryState(reply));
    }

    [Fact]
    public void PushTelemetryStateReturnsFalseWhenRequestHasNoMessageId()
    {
        using var request = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "urn:request");

        Assert.False(RequestTelemetryStateTracker.PushTelemetryState(request, new RequestTelemetryState(), TimeSpan.FromHours(1), _ => { }));
    }

    [Fact]
    public void TryMarkCompletedOnlySucceedsOnce()
    {
        var state = new RequestTelemetryState();

        Assert.True(state.TryMarkCompleted());
        Assert.False(state.TryMarkCompleted());
    }
}
