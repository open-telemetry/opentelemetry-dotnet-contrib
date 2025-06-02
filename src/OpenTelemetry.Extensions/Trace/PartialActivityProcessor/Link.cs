// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.PartialActivityProcessor;

/// <summary>
/// Link per spec.
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
    public string? TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier of the link.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    /// Gets or sets the trace state of the link.
    /// </summary>
    public string? TraceState { get; set; }

    /// <summary>
    /// Gets the attributes of the link.
    /// </summary>
    public Collection<KeyValue> Attributes { get; } = [];

    /// <summary>
    /// Gets or sets the flags of the link.
    /// </summary>
    public uint? Flags { get; set; }
}
