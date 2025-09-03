// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class PackagesAvailableMessage : IOpAmpMessage
{
    public PackagesAvailableMessage(PackagesAvailable packageAvailable)
    {
        this.PackagesAvailable = packageAvailable;
    }

    public PackagesAvailable PackagesAvailable { get; set; }
}
