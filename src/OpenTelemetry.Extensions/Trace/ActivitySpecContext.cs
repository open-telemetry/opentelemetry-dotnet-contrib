// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace;

/// <summary>
/// Represents the context of an activity spec.
/// </summary>
/// <param name="activityContext">Activity context.</param>
public class ActivitySpecContext(ActivityContext activityContext)
{
    /// <summary>
    /// Gets or sets name of the activity.
    /// </summary>
    public string? TraceId { get; } = activityContext.TraceId.ToString();

    /// <summary>
    /// Gets or sets the trace flags.
    /// </summary>
    public string? SpanId { get; } = activityContext.SpanId.ToString();
}
