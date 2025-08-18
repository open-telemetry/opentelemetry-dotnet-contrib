// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// TracesData per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class TracesData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TracesData"/> class with a single resource span.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal.</param>
    public TracesData(Activity activity, Signal signal)
    {
        this.ResourceSpans.Add(new ResourceSpans(activity, signal));
    }

    /// <summary>
    /// Signal enum to indicate whether the activity is starting or stopping.
    /// </summary>
    public enum Signal
    {
        /// <summary>
        /// Signal indicating the start of an activity.
        /// </summary>
        Heartbeat,

        /// <summary>
        /// Signal indicating the stop of an activity.
        /// </summary>
        Ended,
    }

    /// <summary>
    /// Gets the resource spans.
    /// </summary>
    [JsonPropertyName("resource_spans")]
    [JsonInclude]
    internal ICollection<ResourceSpans> ResourceSpans { get; } = new List<ResourceSpans>();
}
