// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client;

internal sealed class FrameBuilder : IFrameBuilder
{
    private readonly OpAmpClientSettings settings;

    private AgentToServer? currentMessage;
    private ByteString instanceUid;
    private ulong sequenceNum;

    public FrameBuilder(OpAmpClientSettings settings)
    {
        this.settings = settings;
        this.instanceUid = ByteString.CopyFrom(this.settings.InstanceUid.ToByteArray());
    }

    public IFrameBuilder StartBaseMessage()
    {
        if (this.currentMessage != null)
        {
            throw new InvalidOperationException("Message base is already initialized.");
        }

        var message = new AgentToServer()
        {
            InstanceUid = this.instanceUid,
            SequenceNum = ++this.sequenceNum,
        };

        this.currentMessage = message;
        return this;
    }

    AgentToServer IFrameBuilder.Build()
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        var message = this.currentMessage;
        this.currentMessage = null; // Reset for the next message

        return message;
    }

    public void Reset()
    {
        this.currentMessage = null;
    }
}
