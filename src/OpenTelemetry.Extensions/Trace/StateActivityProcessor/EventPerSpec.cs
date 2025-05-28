// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// Event per spec.
/// </summary>
public class EventPerSpec
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPerSpec"/> class.
    /// </summary>
    /// <param name="activityEvent">Event from activity.</param>
    public EventPerSpec(ActivityEvent activityEvent)
    {
        this.TimeUnixNano =
            SpecHelper.ToUnixTimeNanoseconds(activityEvent.Timestamp.DateTime.ToUniversalTime());
        this.Name = activityEvent.Name;
        foreach (var activityEventTag in activityEvent.Tags)
        {
            var keyValue = new KeyValue
            {
                Key = activityEventTag.Key,
                Value = new AnyValue(activityEventTag.Value?.ToString()),
            };
            this.Attributes.Add(keyValue);
        }
    }

    /// <summary>
    /// Gets or sets the time in Unix nanoseconds when the event occurred.
    /// </summary>
    public ulong? TimeUnixNano { get; set; }

    /// <summary>
    /// Gets or sets the name of the event.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the attributes of the event.
    /// </summary>
    public Collection<KeyValue> Attributes { get; } = [];
}
