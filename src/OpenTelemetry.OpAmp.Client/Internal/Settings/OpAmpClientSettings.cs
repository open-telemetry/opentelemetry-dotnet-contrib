// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Settings;

internal class OpAmpClientSettings
{
    /// <summary>
    /// Gets or sets the unique identifier for the current instance.
    /// </summary>
    public Guid InstanceUid { get; set; }
#if NET9_0_OR_GREATER
        = Guid.CreateVersion7();
#else
        = Guid.NewGuid();
#endif

    /// <summary>
    /// Gets or sets the chosen metrics schema to write.
    /// </summary>
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Http;

    /// <summary>
    /// Gets or sets the server URL to connect to.
    /// </summary>
    public Uri ServerUrl { get; set; } = new("https://localhost:4320/v1/opamp");

    public HeartbeatSettings Heartbeat { get; set; } = new();
}
