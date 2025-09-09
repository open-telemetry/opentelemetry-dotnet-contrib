// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Settings;

internal sealed class HeartbeatSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the heartbeat is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the time interval for the operation.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30); // Default to 30 seconds
}
