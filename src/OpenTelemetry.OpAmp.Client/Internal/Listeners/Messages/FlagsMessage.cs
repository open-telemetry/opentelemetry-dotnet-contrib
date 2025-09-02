// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class FlagsMessage : IOpAmpMessage
{
    public FlagsMessage(ServerToAgentFlags flags)
    {
        this.Flags = flags;
    }

    public ServerToAgentFlags Flags { get; set; }
}
