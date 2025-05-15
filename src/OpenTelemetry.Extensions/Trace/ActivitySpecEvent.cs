// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.Trace;

/// <summary>
/// Represents an event per spec associated with an activity.
/// </summary>
public class ActivitySpecEvent
{
    /// <summary>
    /// Gets name of the event.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets timestamp of the event.
    /// </summary>
    public string? Timestamp { get; }

    /// <summary>
    /// Gets attributes of the event.
    /// </summary>
    public Dictionary<string, object>? Attributes { get; }
}
