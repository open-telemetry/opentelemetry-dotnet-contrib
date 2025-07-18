// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;

namespace OpenTelemetry.OpAmp.Client.Listeners.Messages;

internal class CapabilitiesMessage : IOpAmpMessage
{
    public CapabilitiesMessage(ServerCapabilities capabilities)
    {
        this.Capabilities = capabilities;
    }

    public ServerCapabilities Capabilities { get; set; }
}
