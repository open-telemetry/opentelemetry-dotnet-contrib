// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// AnyValue per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/common/v1/common.proto.
/// </summary>
public class AnyValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnyValue"/> class.
    /// </summary>
    /// <param name="stringValue">Nullable string value.</param>
    public AnyValue(string? stringValue)
    {
        this.StringValue = stringValue;
    }

    /// <summary>
    /// Gets or sets the string value.
    /// </summary>
    [JsonPropertyName("string_value")]
    [JsonInclude]
    internal string? StringValue { get; set; }
}
