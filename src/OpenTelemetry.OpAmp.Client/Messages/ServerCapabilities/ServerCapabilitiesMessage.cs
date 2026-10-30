// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmpProto = OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Server sent capabilities message.
/// </summary>
public class ServerCapabilitiesMessage : OpAmpMessage
{
    internal ServerCapabilitiesMessage(OpAmpProto.ServerCapabilities capabilities)
        : this((ulong)capabilities)
    {
    }

    internal ServerCapabilitiesMessage(ulong capabilities)
    {
        // The capabilities are 64 bits wide on the wire, which is wider than the enum which defines the known ones.
        this.Capabilities = (ServerSentCapabilities)capabilities;
    }

    /// <summary>
    /// Gets the capabilities sent by the server.
    /// </summary>
    public ServerSentCapabilities Capabilities { get; }
}
