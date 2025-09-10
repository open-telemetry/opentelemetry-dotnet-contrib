// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using OpAmp.Proto.V1;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Listeners;
using OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class FrameProcessor
{
    private readonly ConcurrentDictionary<Type, ImmutableList<IOpAmpListener>> listeners = [];

    public void Subscribe<T>(IOpAmpListener<T> listener)
        where T : IOpAmpMessage
    {
        Guard.ThrowIfNull(listener, nameof(listener));

        // It is expected to be much more read-heavy than write-heavy, so we use ImmutableList for thread safety
        this.listeners.AddOrUpdate(
            typeof(T),
            _ => [listener],
            (_, list) => list.Add(listener));
    }

    public void Unsubscribe<T>(IOpAmpListener<T> listener)
        where T : IOpAmpMessage
    {
        Guard.ThrowIfNull(listener, nameof(listener));

        this.listeners.AddOrUpdate(
            typeof(T),
            _ => ImmutableList<IOpAmpListener>.Empty,
            (_, list) =>
            {
                if (list.Count == 1 && list[0] == listener)
                {
                    return ImmutableList<IOpAmpListener>.Empty;
                }

                return list.Remove(listener);
            });
    }

    public void OnServerFrame(ReadOnlySequence<byte> sequence)
    {
        this.Deserialize(sequence);
    }

    public void OnServerFrame(ReadOnlySequence<byte> sequence, int count, bool verifyHeader)
    {
        var headerSize = 0;

        // verify and decode
        if (verifyHeader)
        {
            if (!OpAmpWsHeaderHelper.TryVerifyHeader(sequence, out headerSize, out string errorMessage))
            {
                // TODO: log error message

                return;
            }
        }

        this.Deserialize(sequence, count, headerSize);
    }

    private void Deserialize(ReadOnlySequence<byte> sequence, int count, int headerSize)
    {
        var dataSegment = sequence.Slice(headerSize, count - headerSize);
        this.Deserialize(dataSegment);
    }

    private void Deserialize(ReadOnlySequence<byte> sequence)
    {
        var message = ServerToAgent.Parser.ParseFrom(sequence);

        if (message.ErrorResponse != null)
        {
            this.Dispatch(new ErrorResponseMessage(message.ErrorResponse));
        }

        if (message.RemoteConfig != null)
        {
            this.Dispatch(new RemoteConfigMessage(message.RemoteConfig));
        }

        if (message.ConnectionSettings != null)
        {
            this.Dispatch(new ConnectionSettingsMessage(message.ConnectionSettings));
        }

        if (message.PackagesAvailable != null)
        {
            this.Dispatch(new PackagesAvailableMessage(message.PackagesAvailable));
        }

        if (message.Flags != 0)
        {
            this.Dispatch(new FlagsMessage((ServerToAgentFlags)message.Flags));
        }

        if (message.Capabilities != 0)
        {
            this.Dispatch(new CapabilitiesMessage((ServerCapabilities)message.Capabilities));
        }

        if (message.AgentIdentification != null)
        {
            this.Dispatch(new AgentIdentificationMessage(message.AgentIdentification));
        }

        if (message.Command != null)
        {
            this.Dispatch(new CommandMessage(message.Command));
        }

        if (message.CustomCapabilities != null)
        {
            this.Dispatch(new CustomCapabilitiesMessage(message.CustomCapabilities));
        }

        if (message.CustomMessage != null)
        {
            this.Dispatch(new CustomMessageMessage(message.CustomMessage));
        }
    }

    private void Dispatch<T>(T message)
        where T : IOpAmpMessage
    {
        if (this.listeners.TryGetValue(typeof(T), out var list))
        {
            foreach (var listener in list)
            {
                if (listener is IOpAmpListener<T> typedListener)
                {
                    typedListener.HandleMessage(message);
                }
            }
        }
    }
}
