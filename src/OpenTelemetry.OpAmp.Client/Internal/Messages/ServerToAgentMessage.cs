// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal.Messages;

internal sealed class ServerToAgentMessage : OpAmpMessage
{
    public ServerToAgentMessage(ServerToAgent message)
    {
        this.Message = message;
    }

    public ServerToAgent Message { get; }
}
