// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents an agent configuration file.
/// </summary>
public class AgentConfigFile
{
    internal AgentConfigFile(global::OpAmp.Proto.V1.AgentConfigFile agentConfigFile)
    {
        this.Body = agentConfigFile.Body?.ToByteArray() ?? [];
        this.ContentType = agentConfigFile.ContentType;
    }

    /// <summary>
    /// Gets the raw bytes of the configuration file.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public byte[] Body { get; }
#pragma warning restore CA1819 // Properties should not return arrays

    /// <summary>
    /// Gets the MIME Content-Type that describes the data contained in the <see cref="Body"/>".
    /// </summary>
    public string? ContentType { get; }
}
