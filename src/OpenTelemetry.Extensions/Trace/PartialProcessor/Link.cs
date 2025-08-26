// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// Link per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class Link
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Link"/> class.
    /// </summary>
    /// <param name="activityLink">Activity link.</param>
    public Link(ActivityLink activityLink)
    {
        this.TraceId = activityLink.Context.TraceId.ToHexString();
        this.SpanId = activityLink.Context.SpanId.ToHexString();
        this.TraceState = activityLink.Context.TraceState;
        if (activityLink.Tags != null)
        {
            foreach (var activityLinkTag in activityLink.Tags)
            {
                var keyValue = new KeyValue
                {
                    Key = activityLinkTag.Key,
                    Value = new AnyValue(activityLinkTag.Value?.ToString()),
                };
                this.Attributes.Add(keyValue);
            }
        }

        this.Flags = activityLink.Context.TraceFlags switch
        {
            ActivityTraceFlags.None => 0,
            ActivityTraceFlags.Recorded => 1,
            _ => 0,
        };
    }

    /// <summary>
    /// Gets or sets the trace identifier of the link.
    /// </summary>
    [JsonPropertyName("trace_id")]
    [JsonInclude]
    internal string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier of the link.
    /// </summary>
    [JsonPropertyName("span_id")]
    [JsonInclude]
    internal string? SpanId { get; set; }

    /// <summary>
    /// Gets or sets the trace state of the link.
    /// </summary>
    [JsonPropertyName("trace_state")]
    [JsonInclude]
    internal string? TraceState { get; set; }

    /// <summary>
    /// Gets the attributes of the link.
    /// </summary>
    [JsonPropertyName("attributes")]
    [JsonInclude]
    internal ICollection<KeyValue> Attributes { get; } = new List<KeyValue>();

    /// <summary>
    /// Gets or sets the flags of the link.
    /// </summary>
    [JsonPropertyName("flags")]
    [JsonInclude]
    internal uint? Flags { get; set; }
}
