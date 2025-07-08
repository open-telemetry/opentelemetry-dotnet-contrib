// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Settings;

/// <summary>
/// Represents the configuration settings for a heartbeat operation.
/// </summary>
public class HeartbeatSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the heartbeat is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the service should wait for the first status update before proceeding.
    /// </summary>
    public bool ShouldWaitForFirstStatus { get; set; }

    /// <summary>
    /// Gets or sets the initial status of the agent.
    /// </summary>
    public string InitialStatus { get; set; } = "OK"; // Default to "OK"

    /// <summary>
    /// Gets or sets the time interval for the operation.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30); // Default to 30 seconds
}
