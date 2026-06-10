// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmpProto = OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Messages;

internal class ServerCapabilitiesMessage : OpAmpMessage
{
    public ServerCapabilitiesMessage(OpAmpProto.ServerCapabilities capabilities)
    {
        this.Capabilities = (ServerSentCapabilities)capabilities;
    }

    public ServerSentCapabilities Capabilities { get; }
}
