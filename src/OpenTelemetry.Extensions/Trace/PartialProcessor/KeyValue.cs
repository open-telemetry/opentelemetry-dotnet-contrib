// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace OpenTelemetry.Extensions.Trace.PartialProcessor;

/// <summary>
/// KeyValue per spec https://github.com/open-telemetry/opentelemetry-proto/blob/main/opentelemetry/proto/common/v1/common.proto.
/// </summary>
public class KeyValue
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    [JsonPropertyName("key")]
    [JsonInclude]
    internal string? Key { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonInclude]
    internal AnyValue? Value { get; set; }
}
