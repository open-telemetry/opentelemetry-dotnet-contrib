// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using OpAmp.Protocol;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Listeners.Messages;
using OpenTelemetry.OpAmp.Client.Utils;

namespace OpenTelemetry.OpAmp.Client;

internal class FrameProcessor
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

    public void OnServerFrame(ReadOnlySequence<byte> sequence, int count, bool verifyHeader)
    {
        var headerSize = 0;

        // verify and decode
        if (verifyHeader)
        {
            var headerSegment = SequenceHelper.GetHeaderSegment(sequence);
            if (!OpAmpWsHeaderHelper.TryVerifyHeader(headerSegment, out headerSize))
            {
                return;
            }
        }

        this.Deserialize(sequence, count, headerSize);
    }

    private void Deserialize(ReadOnlySequence<byte> sequence, int count, int headerSize)
    {
        var dataSegment = sequence.Slice(headerSize, count - headerSize);
        var message = ServerToAgent.Parser.ParseFrom(dataSegment);

        if (message.ErrorResponse != null)
        {
            this.Dispatch(new ErrorResponseMessage() { ErrorResponse = message.ErrorResponse });
        }

        if (message.RemoteConfig != null)
        {
            this.Dispatch(new RemoteConfigMessage() { RemoteConfig = message.RemoteConfig });
        }

        if (message.ConnectionSettings != null)
        {
            this.Dispatch(new ConnectionSettingsMessage() { ConnectionSettings = message.ConnectionSettings });
        }

        if (message.PackagesAvailable != null)
        {
            this.Dispatch(new PackagesAvailableMessage() { PackagesAvailable = message.PackagesAvailable });
        }

        if (message.Flags != 0)
        {
            this.Dispatch(new FlagsMessage() { Flags = (ServerToAgentFlags)message.Flags });
        }

        if (message.Capabilities != 0)
        {
            this.Dispatch(new CapabilitiesMessage() { Capabilities = (ServerCapabilities)message.Capabilities });
        }

        if (message.AgentIdentification != null)
        {
            this.Dispatch(new AgentIdentificationMessage() { AgentIdentification = message.AgentIdentification });
        }

        if (message.Command != null)
        {
            this.Dispatch(new CommandMessage() { Command = message.Command });
        }

        if (message.CustomCapabilities != null)
        {
            this.Dispatch(new CustomCapabilitiesMessage() { CustomCapabilities = message.CustomCapabilities });
        }

        if (message.CustomMessage != null)
        {
            this.Dispatch(new CustomMessageMessage() { CustomMessage = message.CustomMessage });
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
