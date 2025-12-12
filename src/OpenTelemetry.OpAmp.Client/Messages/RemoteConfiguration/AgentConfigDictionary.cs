// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents a collection of agent configuration files, indexed by agent name.
/// </summary>
public class AgentConfigDictionary : Dictionary<string, AgentConfigFile>
{
    internal AgentConfigDictionary()
    {
    }
}
