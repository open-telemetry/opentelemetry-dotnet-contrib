// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using OpAmp.Proto.V1;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Messages;
using OpenTelemetry.OpAmp.Client.Internal.Utils;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class FrameProcessor
{
    private readonly ConcurrentDictionary<Type, IReadOnlyList<object>> listeners = [];

    public void Subscribe<T>(IOpAmpListener<T> listener)
        where T : OpAmpMessage
    {
        Guard.ThrowIfNull(listener, nameof(listener));

        // It is expected to be much more read-heavy than write-heavy, so we use ImmutableList for thread safety
        this.listeners.AddOrUpdate(
            typeof(T),
            _ => [listener],
            (_, list) =>
            {
                var newList = new List<object>(list.Count + 1);
                newList.AddRange(list);
                newList.Add(listener);
                return newList;
            });
    }

    public void Unsubscribe<T>(IOpAmpListener<T> listener)
        where T : OpAmpMessage
    {
        Guard.ThrowIfNull(listener, nameof(listener));

        this.listeners.AddOrUpdate(
            typeof(T),
            _ => [],
            (_, list) =>
            {
                return list.Count == 1 && list[0] is IOpAmpListener<T> typedListener && ReferenceEquals(typedListener, listener)
                    ? []
                    : [.. list.Where(x => !ReferenceEquals(x, listener))];
            });
    }

    public void OnServerFrame(ReadOnlySequence<byte> sequence)
        => this.Deserialize(sequence);

    public void OnServerFrame(ReadOnlySequence<byte> sequence, int count, bool verifyHeader)
    {
        var headerSize = 0;

        // verify and decode
        if (verifyHeader)
        {
            if (!OpAmpWsHeaderHelper.TryVerifyHeader(sequence, out headerSize, out var errorMessage))
            {
                OpAmpClientEventSource.Log.InvalidWsFrame(errorMessage);

                return;
            }
        }

        this.Deserialize(sequence, count, headerSize);
    }

    private static void Dispatch<T>(T message, IReadOnlyList<object> listeners)
        where T : OpAmpMessage
    {
        foreach (var listener in listeners)
        {
            if (listener is IOpAmpListener<T> typedListener)
            {
                try
                {
                    typedListener.HandleMessage(message);
                }
                catch (Exception ex)
                {
                    OpAmpClientEventSource.Log.FrameProcessingException(ex);
                }
            }
        }
    }

    private void Deserialize(ReadOnlySequence<byte> sequence, int count, int headerSize)
    {
        var dataSegment = sequence.Slice(headerSize, count - headerSize);
        this.Deserialize(dataSegment);
    }

    private void Deserialize(ReadOnlySequence<byte> sequence)
    {
        var message = ServerToAgent.Parser.ParseFrom(sequence);

        if (this.TryGetListeners<ServerToAgentMessage>(out var listeners))
        {
            Dispatch(new ServerToAgentMessage(message), listeners);
        }

        if (message.ErrorResponse is { } errorResponse &&
            this.TryGetListeners<ErrorResponseMessage>(out var errorListeners))
        {
            Dispatch(new ErrorResponseMessage(errorResponse), errorListeners);
        }

        if (message.RemoteConfig is { } remoteConfig &&
            this.TryGetListeners<RemoteConfigMessage>(out var remoteConfigListeners))
        {
            Dispatch(new RemoteConfigMessage(remoteConfig), remoteConfigListeners);
        }

        if (message.ConnectionSettings is { } connectionSettings &&
            this.TryGetListeners<ConnectionSettingsMessage>(out var connectionSettingsListeners))
        {
            Dispatch(new ConnectionSettingsMessage(connectionSettings), connectionSettingsListeners);
        }

        if (message.PackagesAvailable is { } packagesAvailable &&
            this.TryGetListeners<PackagesAvailableMessage>(out var packagesListeners))
        {
            Dispatch(new PackagesAvailableMessage(packagesAvailable), packagesListeners);
        }

        if (message.Flags is var flags and not 0 &&
            this.TryGetListeners<FlagsMessage>(out var flagsListeners))
        {
            Dispatch(new FlagsMessage((ServerToAgentFlags)flags), flagsListeners);
        }

        if (message.Capabilities is var capabilities and not 0 &&
            this.TryGetListeners<ServerCapabilitiesMessage>(out var capabilitiesListeners))
        {
            Dispatch(new ServerCapabilitiesMessage((ServerCapabilities)capabilities), capabilitiesListeners);
        }

        if (message.AgentIdentification is { } agentIdentification &&
            this.TryGetListeners<AgentIdentificationMessage>(out var agentIdentificationListeners))
        {
            Dispatch(new AgentIdentificationMessage(agentIdentification), agentIdentificationListeners);
        }

        if (message.Command is { } command &&
            this.TryGetListeners<CommandMessage>(out var commandListeners))
        {
            Dispatch(new CommandMessage(command), commandListeners);
        }

        if (message.CustomCapabilities is { } customCapabilities &&
            this.TryGetListeners<CustomCapabilitiesMessage>(out var customCapabilitiesListeners))
        {
            Dispatch(new CustomCapabilitiesMessage(customCapabilities), customCapabilitiesListeners);
        }

        if (message.CustomMessage is { } customMessage &&
            this.TryGetListeners<CustomMessageMessage>(out var customMessageListeners))
        {
            Dispatch(new CustomMessageMessage(customMessage), customMessageListeners);
        }
    }

    private bool TryGetListeners<T>(out IReadOnlyList<object> result)
        where T : OpAmpMessage
    {
        if (this.listeners.TryGetValue(typeof(T), out var listeners) &&
            listeners.Count != 0)
        {
            result = listeners;
            return true;
        }

        result = Array.Empty<object>();
        return false;
    }
}
