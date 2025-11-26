// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.Kusto;

/// <summary>
/// Options for Kusto instrumentation.
/// </summary>
public class KustoInstrumentationOptions
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

    // TODO: Add flag for query parameter tracing
}
