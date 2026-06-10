// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Server sent flags message.
/// </summary>
public class FlagsMessage : OpAmpMessage
{
    internal FlagsMessage(ServerToAgentFlags flags)
    {
        this.Flags = (ServerCommands)flags;
    }

    /// <summary>
    /// Gets the flags sent by the server.
    /// </summary>
    public ServerCommands Flags { get; }
}
