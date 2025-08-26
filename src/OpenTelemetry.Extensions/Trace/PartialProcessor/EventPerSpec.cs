// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// Event per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
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
            SpecHelper.ToUnixTimeNanoseconds(activityEvent.Timestamp);
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
    [JsonPropertyName("time_unix_nano")]
    [JsonInclude]
    internal ulong? TimeUnixNano { get; set; }

    /// <summary>
    /// Gets or sets the name of the event.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonInclude]
    internal string? Name { get; set; }

    /// <summary>
    /// Gets the attributes of the event.
    /// </summary>
    [JsonPropertyName("attributes")]
    [JsonInclude]
    internal ICollection<KeyValue> Attributes { get; } = new List<KeyValue>();
}
