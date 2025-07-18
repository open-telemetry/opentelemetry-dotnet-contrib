// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Protocol;
using OpenTelemetry.OpAmp.Client.Transport;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockTransport : IOpAmpTransport
{
    private readonly List<AgentToServer> messages = [];

    public IReadOnlyCollection<AgentToServer> Messages => this.messages.AsReadOnly();

    public Task SendAsync<T>(T message, CancellationToken token)
        where T : IMessage<T>
    {
        if (message is AgentToServer agentToServer)
        {
            this.messages.Add(agentToServer);
        }
        else
        {
            throw new InvalidOperationException("Unsupported message type. Only AgentToServer messages are supported.");
        }

        return Task.CompletedTask;
    }
}
