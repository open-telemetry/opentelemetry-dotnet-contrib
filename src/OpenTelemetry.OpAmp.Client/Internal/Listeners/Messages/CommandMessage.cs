// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class CommandMessage : IOpAmpMessage
{
    public CommandMessage(ServerToAgentCommand command)
    {
        this.Command = command;
    }

    public ServerToAgentCommand Command { get; set; }
}
