// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;

internal class RemoteConfigMessage : IOpAmpMessage
{
    public RemoteConfigMessage(AgentRemoteConfig agentRemoteConfig)
    {
        this.RemoteConfig = agentRemoteConfig;
    }

    public AgentRemoteConfig RemoteConfig { get; set; }
}
