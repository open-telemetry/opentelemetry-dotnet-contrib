// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Data;

namespace OpenTelemetry.OpAmp.Client.Settings;

/// <summary>
/// Represents the configuration settings for an OpenTelemetry OpAMP (Open Agent Management Protocol) client.
/// </summary>
public class OpAmpSettings
{
    /// <summary>
    /// Gets or sets the unique identifier for the current instance.
    /// </summary>
    public Guid InstanceUid { get; set; } = Guid.NewGuid(); // TODO: use Guid.CreateVersion7() with .NET 9+

    /// <summary>
    /// Gets or sets the chosen metrics schema to write.
    /// </summary>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.WebSocket;

    /// <summary>
    /// Gets or sets the collection of resources associated with the application.
    /// </summary>
    public OpAmpClientResources Resources { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration settings for the heartbeat mechanism.
    /// </summary>
    public HeartbeatSettings HeartbeatSettings { get; set; } = new();
}
