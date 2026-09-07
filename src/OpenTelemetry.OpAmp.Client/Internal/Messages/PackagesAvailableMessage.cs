// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal.Messages;

internal sealed class PackagesAvailableMessage : OpAmpMessage
{
    public PackagesAvailableMessage(PackagesAvailable packageAvailable)
    {
        this.PackagesAvailable = packageAvailable;
    }

    public PackagesAvailable PackagesAvailable { get; }
}
