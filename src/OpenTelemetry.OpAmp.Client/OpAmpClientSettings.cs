// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client;

/// <summary>
/// Specifies the type of transport protocol to be used for communication.
/// </summary>
/// <remarks>This enumeration defines the available transport protocols for communication. Use <see
/// cref="WebSocket"/> for WebSocket-based communication, or <see cref="Http"/> for
/// HTTP-based communication.</remarks>
internal enum ConnectionType
{
    /// <summary>
    /// Use HTTP transport.
    /// </summary>
    Http = 0,

    /// <summary>
    /// Use WebSocket transport.
    /// </summary>
    WebSocket = 1,
}

internal class OpAmpClientSettings
{
    /// <summary>
    /// Gets or sets the unique identifier for the current instance.
    /// </summary>
    public Guid InstanceUid { get; set; } = Guid.NewGuid(); // TODO: use Guid.CreateVersion7() with .NET 9+

    /// <summary>
    /// Gets or sets the chosen metrics schema to write.
    /// </summary>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Http;

    /// <summary>
    /// Gets or sets the server URL to connect to.
    /// </summary>
    public Uri ServerUrl { get; set; } = new("https://localhost:4320/v1/opamp");
}
