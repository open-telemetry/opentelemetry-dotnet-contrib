// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// ResourceSpans per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class ResourceSpans
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceSpans"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal whether it is start or stop.</param>
    public ResourceSpans(Activity activity, TracesData.Signal signal)
    {
        this.ScopeSpans.Add(new ScopeSpans(activity, signal));
    }

    /// <summary>
    /// Gets the resource attributes.
    /// </summary>
    [JsonPropertyName("scope_spans")]
    [JsonInclude]
    internal ICollection<ScopeSpans> ScopeSpans { get; } = new List<ScopeSpans>();
}
