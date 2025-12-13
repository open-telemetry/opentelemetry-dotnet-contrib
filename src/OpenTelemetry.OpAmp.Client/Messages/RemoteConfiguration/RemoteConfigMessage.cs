// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents a server-to-agent remote configuration message.
/// </summary>
public class RemoteConfigMessage : OpAmpMessage
{
    internal RemoteConfigMessage(AgentRemoteConfig agentRemoteConfig)
    {
        foreach (var config in agentRemoteConfig.Config.ConfigMap)
        {
            this.AgentConfigMap ??= [];
            this.AgentConfigMap[config.Key] = new AgentConfigFile(config.Value);
        }
    }

    /// <inheritdoc cref="AgentConfigDictionary"/>
    public AgentConfigDictionary AgentConfigMap { get; } = [];
}
