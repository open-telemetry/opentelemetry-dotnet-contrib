// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Options for Kusto meter instrumentation.
/// </summary>
public sealed class KustoMeterInstrumentationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the query text should be recorded as an attribute on the activity.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool RecordQueryText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a summary of the query should be recorded as an attribute on the activity.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool RecordQuerySummary { get; set; } = true;
}
