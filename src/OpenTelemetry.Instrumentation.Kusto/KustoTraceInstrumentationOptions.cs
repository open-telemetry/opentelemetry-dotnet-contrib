// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Trace;

/// <summary>
/// Options for Kusto trace instrumentation.
/// </summary>
public sealed class KustoTraceInstrumentationOptions
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

    /// <summary>
    /// Gets or sets an action to enrich the Activity with additional information from the TraceRecord.
    /// </summary>
    /// <remarks>
    /// The second parameter is the raw <see cref="KustoUtils.TraceRecord"/> from the Kusto client library, exposed
    /// directly rather than behind a wrapper. Code that reads its members is therefore coupled to the Kusto client
    /// and may need to change if the client's tracing types change.
    /// </remarks>
    public Action<Activity, KustoUtils.TraceRecord>? Enrich { get; set; }
}
