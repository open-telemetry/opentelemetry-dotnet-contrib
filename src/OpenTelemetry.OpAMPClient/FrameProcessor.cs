// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Opamp.Protocol;
using OpenTelemetry.OpAMPClient.Utils;

namespace OpenTelemetry.OpAMPClient;

internal class FrameProcessor
{
    private readonly IOpAMPMessageListener listener;

    public FrameProcessor(IOpAMPMessageListener listener)
    {
        this.listener = listener;
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
            this.listener.OnErrorResponseReceived(message.ErrorResponse);
        }

        if (message.RemoteConfig != null)
        {
            this.listener.OnSettingsReceived(message.RemoteConfig);
        }

        if (message.ConnectionSettings != null)
        {
            this.listener.OnConnectionSettingsReceived(message.ConnectionSettings);
        }

        if (message.PackagesAvailable != null)
        {
            this.listener.OnPackagesAvailableReceived(message.PackagesAvailable);
        }

        if (message.Flags != 0)
        {
            Console.WriteLine($"TODO: On flags received - {message.Flags}");
        }

        if (message.Capabilities != 0)
        {
            Console.WriteLine($"TODO: On capabilities received - {message.Capabilities}");
        }

        if (message.AgentIdentification != null)
        {
            Console.WriteLine($"TODO: On agent re-identification received - {message.AgentIdentification.NewInstanceUid}");
        }

        if (message.Command != null)
        {
            Console.WriteLine($"TODO: On agent command received - {message.Command.Type.ToString()}");
        }

        if (message.CustomCapabilities != null)
        {
            this.listener.OnCustomCapabilitiesReceived(message.CustomCapabilities);
        }

        if (message.CustomMessage != null)
        {
            this.listener.OnCustomMessageReceived(message.CustomMessage);
        }
    }
}
