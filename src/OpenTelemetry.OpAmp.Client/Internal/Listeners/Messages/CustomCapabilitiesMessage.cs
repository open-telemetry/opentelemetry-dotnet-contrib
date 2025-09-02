// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class CustomCapabilitiesMessage : IOpAmpMessage
{
    public CustomCapabilitiesMessage(CustomCapabilities customCapabilities)
    {
        this.CustomCapabilities = customCapabilities;
    }

    public CustomCapabilities CustomCapabilities { get; set; }
}
