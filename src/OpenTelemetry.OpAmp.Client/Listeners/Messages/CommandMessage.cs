// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal class CommandMessage : IOpAmpMessage
{
    public CommandMessage(ServerToAgentCommand command)
    {
        this.Command = command;
    }

    public ServerToAgentCommand Command { get; set; }
}
