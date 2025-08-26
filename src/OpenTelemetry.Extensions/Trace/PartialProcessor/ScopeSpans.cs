// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// ScopeSpans per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class ScopeSpans
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeSpans"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal whether it is start or stop.</param>
    public ScopeSpans(Activity activity, TracesData.Signal signal)
    {
        this.Scope = new InstrumentationScope(activity);
        this.Spans.Add(new Span(activity, signal));
    }

    /// <summary>
    /// Gets or sets the instrumentation scope.
    /// </summary>
    [JsonPropertyName("scope")]
    [JsonInclude]
    internal InstrumentationScope? Scope { get; set; }

    /// <summary>
    /// Gets the spans in this scope.
    /// </summary>
    [JsonPropertyName("spans")]
    [JsonInclude]
    internal ICollection<Span> Spans { get; } = new List<Span>();
}
