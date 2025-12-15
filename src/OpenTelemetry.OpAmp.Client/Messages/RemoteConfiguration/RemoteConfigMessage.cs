// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents an OpAMP server-to-agent remote configuration message.
/// </summary>
public class RemoteConfigMessage : OpAmpMessage
{
    private readonly Dictionary<string, AgentConfigFile> agentConfigMap;
    private readonly ByteString configHash;

    internal RemoteConfigMessage(AgentRemoteConfig agentRemoteConfig)
    {
        this.agentConfigMap = new Dictionary<string, AgentConfigFile>(agentRemoteConfig.Config.ConfigMap.Count, StringComparer.Ordinal);

        foreach (var config in agentRemoteConfig.Config.ConfigMap)
        {
            if (config.Value is not null)
            {
                this.agentConfigMap[config.Key] = new AgentConfigFile(config.Key, config.Value);
            }
        }

        this.configHash = agentRemoteConfig.ConfigHash;
    }

    /// <summary>
    /// Gets a dictionary of agent configuration files, keyed by the name of the configuration file.
    /// </summary>
    public IReadOnlyDictionary<string, AgentConfigFile> AgentConfigMap => this.agentConfigMap;

    /// <summary>
    /// Gets the length, in bytes, of the configuration hash.
    /// </summary>
    public int HashLength => this.configHash.Length;

    /// <summary>
    /// Returns the configuration hash as a byte array.
    /// </summary>
    /// <returns>A <see cref="byte"/> array containing the hash of the remote configuration.</returns>
    public byte[] GetConfigHashBytes() => this.configHash.ToByteArray();

    /// <summary>
    /// Returns the configuration hash as a UTF-8 string.
    /// </summary>
    /// <returns>A <see cref="string"/> representing the UTF-8 encoded configuration hash.</returns>
    public string GetConfigHashUtf8String() => this.configHash.ToStringUtf8();

    /// <summary>
    /// Attempts to copy the configuration hash to the specified destination buffer.
    /// </summary>
    /// <remarks>If the hash is empty, no data is written and the method returns <c>true</c> with <paramref name="bytesWritten"/> set to
    /// 0. If the destination buffer is too small to hold the hash, no data is written, <paramref name="bytesWritten"/> is set to
    /// 0, and the method returns <c>false</c>.</remarks>
    /// <param name="destination">The buffer that receives the hash bytes. Must be large enough to hold the entire hash content.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes successfully written to the destination buffer.</param>
    /// <returns><c>true</c> if the hash was successfully copied to the destination buffer or if the hash is empty; otherwise, <c>false</c>.</returns>
    public bool TryGetConfigHash(Span<byte> destination, out int bytesWritten)
    {
        if (this.configHash is null)
        {
            bytesWritten = 0;
            return false;
        }

        if (this.configHash.IsEmpty)
        {
            bytesWritten = 0;
            return true;
        }

        if (destination.Length < this.configHash.Length)
        {
            bytesWritten = 0;
            return false;
        }

        this.configHash.Span.CopyTo(destination);
        bytesWritten = this.configHash.Length;
        return true;
    }
}
