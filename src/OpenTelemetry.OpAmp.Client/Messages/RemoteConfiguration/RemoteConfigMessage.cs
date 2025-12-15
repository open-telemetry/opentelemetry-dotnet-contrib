// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents an OpAMP server-to-agent remote configuration message.
/// </summary>
public class RemoteConfigMessage : OpAmpMessage
{
    private readonly Dictionary<string, AgentConfigFile> agentConfigMap;

    internal RemoteConfigMessage(AgentRemoteConfig agentRemoteConfig)
    {
        this.agentConfigMap = new Dictionary<string, AgentConfigFile>(agentRemoteConfig.Config.ConfigMap.Count, StringComparer.Ordinal);

        foreach (var config in agentRemoteConfig.Config.ConfigMap)
        {
            this.agentConfigMap[config.Key] = new AgentConfigFile(config.Key, config.Value);
        }
    }

    /// <summary>
    /// Gets a dictionary of agent configuration files, keyed by the name of the configuration file.
    /// </summary>
    public IReadOnlyDictionary<string, AgentConfigFile> AgentConfigMap => this.agentConfigMap;
}
