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
        : this((ulong)flags)
    {
    }

    internal FlagsMessage(ulong flags)
    {
        // The flags are 64 bits wide on the wire, which is wider than the enum which defines the known ones.
        this.Flags = (ServerSentFlags)flags;
    }

    /// <summary>
    /// Gets the flags sent by the server.
    /// </summary>
    public ServerSentFlags Flags { get; }
}
