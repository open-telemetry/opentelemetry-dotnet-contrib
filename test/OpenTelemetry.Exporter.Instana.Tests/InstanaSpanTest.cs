// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Exporter.Instana.Tests;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

internal enum SpanKind
{
    ENTRY,
    EXIT,
    INTERMEDIATE,
    NOT_SET,
}

internal class InstanaSpanTransformInfo
{
    public string? StatusCode { get; set; }

    public string? StatusDesc { get; set; }

    public bool HasExceptionEvent { get; set; }

    public bool IsEntrySpan { get; set; }
}

internal class InstanaSpanTest
{
    public InstanaSpanTransformInfo? TransformInfo { get; set; }

    [JsonPropertyName("n")]
    public string? N { get; set; }

    [JsonPropertyName("t")]
    public string? T { get; set; }

    [JsonPropertyName("lt")]
    public string? Lt { get; set; }

    [JsonPropertyName("f")]
    public From? F { get; set; }

    [JsonPropertyName("p")]
    public string? P { get; set; }

    [JsonPropertyName("s")]
    public string? S { get; set; }

    [JsonPropertyName("k")]
    public SpanKind? K { get; set; }

    [JsonPropertyName("data")]
    public Data? Data { get; set; }

    [JsonPropertyName("ts")]
    public long Ts { get; set; }

    [JsonPropertyName("d")]
    public long D { get; set; }

    [JsonPropertyName("tp")]
    public string? Tp { get; set; }

    [JsonPropertyName("ec")]
    public int Ec { get; set; }
}

internal class From
{
    [JsonPropertyName("e")]
    public string? E { get; set; }

    [JsonPropertyName("h")]
    public string? H { get; set; }
}

internal class Data
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Values { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("events")]
    public List<SpanEvent>? Events { get; set; }
}

internal class SpanEvent
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ts")]
    public long Ts { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
