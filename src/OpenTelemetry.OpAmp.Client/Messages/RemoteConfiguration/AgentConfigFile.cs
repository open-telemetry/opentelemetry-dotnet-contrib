// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;

namespace OpenTelemetry.OpAmp.Client.Messages;

/// <summary>
/// Represents an agent configuration file.
/// </summary>
public class AgentConfigFile
{
    private readonly ByteString body;

    internal AgentConfigFile(string name, global::OpAmp.Proto.V1.AgentConfigFile agentConfigFile)
    {
        this.body = agentConfigFile.Body ?? ByteString.Empty;
        this.ContentType = agentConfigFile.ContentType;
        this.Name = name;
    }

    /// <summary>
    /// Gets the length, in bytes, of the configuration file body.
    /// </summary>
    public int BodyLength => this.body.Length;

    /// <summary>
    /// Gets the MIME Content-Type that describes the data contained in the body of the remote configuration file.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the name of this configuration file.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Returns the configuration file body as a byte array.
    /// </summary>
    /// <returns>A byte array containing the contents of the message body. The array is empty if the body has no content.</returns>
    public byte[] GetBodyBytes() => this.body.ToByteArray() ?? [];

    /// <summary>
    /// Attempts to copy the configuration file body to the specified destination buffer.
    /// </summary>
    /// <remarks>If the body is empty, no data is written and the method returns <c>true</c> with <paramref name="bytesWritten"/> set to
    /// 0. If the destination buffer is too small to hold the body content, no data is written, <paramref name="bytesWritten"/> is set to
    /// 0, and the method returns <c>false</c>.</remarks>
    /// <param name="destination">The buffer that receives the body bytes. Must be large enough to hold the entire body content.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes successfully written to the destination buffer.</param>
    /// <returns><c>true</c> if the body was successfully copied to the destination buffer or if the body is empty; otherwise, <c>false</c>.</returns>
    public bool TryGetBody(Span<byte> destination, out int bytesWritten)
    {
        if (this.body.IsEmpty)
        {
            bytesWritten = 0;
            return true;
        }

        if (destination.Length < this.body.Length)
        {
            bytesWritten = 0;
            return false;
        }

        this.body.Span.CopyTo(destination);
        bytesWritten = this.body.Length;
        return true;
    }
}
