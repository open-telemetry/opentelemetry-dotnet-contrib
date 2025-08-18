// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json.Serialization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// Instrumentation scope per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/trace/v1/trace.proto.
/// </summary>
public class InstrumentationScope
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentationScope"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    public InstrumentationScope(Activity activity)
    {
        Guard.ThrowIfNull(activity);

        this.Name = activity.Source.Name;

        this.Version = activity.Source.Version;

        foreach (var keyValuePair in activity.Tags)
        {
            KeyValue keyValue = new KeyValue
            {
                Key = keyValuePair.Key,
                Value = new AnyValue(keyValuePair.Value),
            };
            this.Attributes.Add(keyValue);
        }
    }

    /// <summary>
    /// Gets or sets the name of the instrumentation scope.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonInclude]
    internal string? Name { get; set; }

    /// <summary>
    /// Gets or sets the version of the instrumentation scope.
    /// </summary>
    [JsonPropertyName("version")]
    [JsonInclude]
    internal string? Version { get; set; }

    /// <summary>
    /// Gets the attributes of the instrumentation scope.
    /// </summary>
    [JsonPropertyName("attributes")]
    [JsonInclude]
    internal ICollection<KeyValue> Attributes { get; } = new List<KeyValue>();
}
