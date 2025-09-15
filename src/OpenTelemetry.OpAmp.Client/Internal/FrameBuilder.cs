// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal;

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

    IFrameBuilder IFrameBuilder.AddHeartbeat(HealthReport health)
    {
        if (this.currentMessage == null)
        {
            throw new InvalidOperationException("Message base is not initialized.");
        }

        this.currentMessage.Health = new ComponentHealth()
        {
            Healthy = health.IsHealthy,
            StartTimeUnixNano = health.StartTime,
            StatusTimeUnixNano = health.StatusTime,
        };

        if (health.Status != null)
        {
            this.currentMessage.Health.Status = health.Status;
        }

        if (health.LastError != null)
        {
            this.currentMessage.Health.LastError = health.LastError;
        }

        foreach (var item in health.Components)
        {
            var component = new ComponentHealth()
            {
                Healthy = item.IsHealthy,
                StartTimeUnixNano = (ulong)item.StartTime.ToUnixTimeMilliseconds() * 1_000_000, // Convert to nanoseconds
                StatusTimeUnixNano = (ulong)item.StatusTime.ToUnixTimeMilliseconds() * 1_000_000, // Convert to nanoseconds
            };

            if (health.Status != null)
            {
                component.Status = health.Status;
            }

            if (health.LastError != null)
            {
                component.LastError = health.LastError;
            }

            this.currentMessage.Health.ComponentHealthMap[item.ComponentName] = component;
        }

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
