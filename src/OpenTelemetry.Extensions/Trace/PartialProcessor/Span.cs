// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// Span per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class Span
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Span"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal whether it is start or stop.</param>
    /// <exception cref="ArgumentNullException">Exception if activity is null.</exception>
    public Span(Activity activity, TracesData.Signal signal)
    {
        Guard.ThrowIfNull(activity);

        this.TraceId = activity.TraceId.ToHexString();

        this.SpanId = activity.SpanId.ToHexString();

        this.TraceState = activity.Status.ToString();

        this.ParentSpanId = activity.ParentSpanId.ToHexString();

        this.Flags = (uint)activity.ActivityTraceFlags;

        this.Name = activity.DisplayName;

        this.Kind = activity.Kind switch
        {
            ActivityKind.Internal => SpanKind.Internal,
            ActivityKind.Client => SpanKind.Client,
            ActivityKind.Server => SpanKind.Server,
            ActivityKind.Producer => SpanKind.Producer,
            ActivityKind.Consumer => SpanKind.Consumer,
            _ => SpanKind.Unspecified,
        };

        this.StartTimeUnixNano = SpecHelper.ToUnixTimeNanoseconds(activity.StartTimeUtc);

        this.EndTimeUnixNano = signal == TracesData.Signal.Heartbeat
            ? null
            : SpecHelper.ToUnixTimeNanoseconds(activity.StartTimeUtc.Add(activity.Duration));

        foreach (var activityTagObject in activity.TagObjects)
        {
            var keyValue = new KeyValue
            {
                Key = activityTagObject.Key,
                Value = new AnyValue(activityTagObject.Value?.ToString()),
            };
            this.Attributes.Add(keyValue);
        }

        foreach (var activityEvent in activity.Events)
        {
            this.Events.Add(new EventPerSpec(activityEvent));
        }

        foreach (var activityLink in activity.Links)
        {
            this.Links.Add(new Link(activityLink));
        }

        this.Status = new Status(activity.Status, activity.StatusDescription);
    }

    /// <summary>
    /// Gets or sets the trace identifier of the span.
    /// </summary>
    [JsonPropertyName("trace_id")]
    [JsonInclude]
    internal string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier of the span.
    /// </summary>
    [JsonPropertyName("span_id")]
    [JsonInclude]
    internal string? SpanId { get; set; }

    /// <summary>
    /// Gets or sets the trace state of the span.
    /// </summary>
    [JsonPropertyName("trace_state")]
    [JsonInclude]
    internal string? TraceState { get; set; }

    /// <summary>
    /// Gets or sets the parent span identifier of the span.
    /// </summary>
    [JsonPropertyName("parent_span_id")]
    [JsonInclude]
    internal string? ParentSpanId { get; set; }

    /// <summary>
    /// Gets or sets the flags of the span.
    /// </summary>
    [JsonPropertyName("flags")]
    [JsonInclude]
    internal uint? Flags { get; set; }

    /// <summary>
    /// Gets or sets the name of the span.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonInclude]
    internal string? Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of the span.
    /// </summary>
    [JsonPropertyName("kind")]
    [JsonInclude]
    internal SpanKind? Kind { get; set; }

    /// <summary>
    /// Gets or sets the start time in Unix nanoseconds when the span started.
    /// </summary>
    [JsonPropertyName("start_time_unix_nano")]
    [JsonInclude]
    internal ulong? StartTimeUnixNano { get; set; }

    /// <summary>
    /// Gets or sets the end time in Unix nanoseconds when the span ended.
    /// </summary>
    [JsonPropertyName("end_time_unix_nano")]
    [JsonInclude]
    internal ulong? EndTimeUnixNano { get; set; }

    /// <summary>
    /// Gets the attributes of the span.
    /// </summary>
    [JsonPropertyName("attributes")]
    [JsonInclude]
    internal ICollection<KeyValue> Attributes { get; } = new List<KeyValue>();

    /// <summary>
    /// Gets the events of the span.
    /// </summary>
    [JsonPropertyName("events")]
    [JsonInclude]
    internal ICollection<EventPerSpec> Events { get; } = new List<EventPerSpec>();

    /// <summary>
    /// Gets the links of the span.
    /// </summary>
    [JsonPropertyName("links")]
    [JsonInclude]
    internal ICollection<Link> Links { get; } = new List<Link>();

    /// <summary>
    /// Gets or sets the status of the span.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonInclude]
    internal Status? Status { get; set; }
}
