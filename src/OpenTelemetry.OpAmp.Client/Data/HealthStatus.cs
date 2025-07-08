// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Data;

/// <summary>
/// Represents the overall health status of an agent.
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Gets or sets a value indicating whether the agent is in a healthy state.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the current status of the agent represented as a string.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message if the agent is in erroneous state.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets the collection of health statuses for sub-components.
    /// </summary>
    public IList<ComponentHealthStatus> Components { get; } = [];
}
