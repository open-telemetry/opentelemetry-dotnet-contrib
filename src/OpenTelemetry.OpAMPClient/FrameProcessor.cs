// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Opamp.Protocol;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAMPClient.Listeners;
using OpenTelemetry.OpAMPClient.Listeners.Messages;
using OpenTelemetry.OpAMPClient.Utils;

namespace OpenTelemetry.OpAMPClient;

internal class FrameProcessor
{
    private readonly Dictionary<Type, List<IOpAMPListener>> listeners = [];

    public void Subscribe<T>(IOpAMPListener<T> listener)
        where T : IOpAMPMessage
    {
        Guard.ThrowIfNull(listener, nameof(listener));

        if (!this.listeners.TryGetValue(typeof(T), out var list))
        {
            list = [];
            this.listeners[typeof(T)] = list;
        }

        list.Add(listener);
    }

    public void Unsubscribe<T>(IOpAMPListener<T> listener)
        where T : IOpAMPMessage
    {
        Guard.ThrowIfNull(listener, nameof(listener));

        if (this.listeners.TryGetValue(typeof(T), out var list))
        {
            list.Remove(listener);
        }
    }

    public void OnServerFrame(ReadOnlySequence<byte> sequence, int count, bool verifyHeader)
    {
        var headerSize = 0;

        // verify and decode
        if (verifyHeader)
        {
            var headerSegment = SequenceHelper.GetHeaderSegment(sequence);
            if (!OpAMPWsHeaderHelper.TryVerifyHeader(headerSegment, out headerSize))
            {
                return;
            }
        }

        this.Deserialize(sequence, count, headerSize);
    }

    public void Deserialize(ReadOnlySequence<byte> sequence, int count, int headerSize)
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
        where T : IOpAMPMessage
    {
        if (this.listeners.TryGetValue(typeof(T), out var list))
        {
            foreach (var listener in list)
            {
                if (listener is IOpAMPListener<T> typedListener)
                {
                    typedListener.HandleMessage(message);
                }
            }
        }
    }
}
