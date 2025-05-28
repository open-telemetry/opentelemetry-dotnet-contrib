// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// Span per spec.
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
            ActivityKind.Internal => SpanKind.SpanKindInternal,
            ActivityKind.Client => SpanKind.SpanKindClient,
            ActivityKind.Server => SpanKind.SpanKindServer,
            ActivityKind.Producer => SpanKind.SpanKindProducer,
            ActivityKind.Consumer => SpanKind.SpanKindConsumer,
            _ => SpanKind.SpanKindUnspecified,
        };

        this.StartTimeUnixNano = SpecHelper.ToUnixTimeNanoseconds(activity.StartTimeUtc);

        this.EndTimeUnixNano = signal == TracesData.Signal.Start
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
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier of the span.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    /// Gets or sets the trace state of the span.
    /// </summary>
    public string? TraceState { get; set; }

    /// <summary>
    /// Gets or sets the parent span identifier of the span.
    /// </summary>
    public string? ParentSpanId { get; set; }

    /// <summary>
    /// Gets or sets the flags of the span.
    /// </summary>
    public uint? Flags { get; set; }

    /// <summary>
    /// Gets or sets the name of the span.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of the span.
    /// </summary>
    public SpanKind? Kind { get; set; }

    /// <summary>
    /// Gets or sets the start time in Unix nanoseconds when the span started.
    /// </summary>
    public ulong? StartTimeUnixNano { get; set; }

    /// <summary>
    /// Gets or sets the end time in Unix nanoseconds when the span ended.
    /// </summary>
    public ulong? EndTimeUnixNano { get; set; }

    /// <summary>
    /// Gets the attributes of the span.
    /// </summary>
    public Collection<KeyValue> Attributes { get; } = [];

    /// <summary>
    /// Gets the events of the span.
    /// </summary>
    public Collection<EventPerSpec> Events { get; } = [];

    /// <summary>
    /// Gets the links of the span.
    /// </summary>
    public Collection<Link> Links { get; } = [];

    /// <summary>
    /// Gets or sets the status of the span.
    /// </summary>
    public Status? Status { get; set; }
}
