// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Data;

namespace OpenTelemetry.OpAMPClient.Settings;

/// <summary>
/// Represents the configuration settings for an OpenTelemetry OpAMP (Open Agent Management Protocol) client.
/// </summary>
public class OpAMPSettings
{
    /// <summary>
    /// Gets or sets the chosen metrics schema to write.
    /// </summary>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.WebSocket;

    /// <summary>
    /// Gets or sets the collection of resources associated with the application.
    /// </summary>
    public OpAMPClientResources Resources { get; set; } = new();
}
