// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class RemoteConfigMessage : OpAmpMessage
{
    public RemoteConfigMessage(AgentRemoteConfig agentRemoteConfig)
    {
        this.RemoteConfig = agentRemoteConfig;
    }

    public AgentRemoteConfig RemoteConfig { get; set; }
}
