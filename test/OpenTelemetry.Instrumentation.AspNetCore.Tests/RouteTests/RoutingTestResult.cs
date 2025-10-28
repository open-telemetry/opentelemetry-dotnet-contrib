// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using RouteTests.TestApplication;

namespace RouteTests;

internal abstract class RoutingTestResult
{
    internal static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public string? IdealHttpRoute { get; set; }

    public RouteInfo RouteInfo { get; set; } = new();

    [JsonIgnore]
    public TestCase TestCase { get; set; } = new();
}

#pragma warning disable SA1402 // File may only contain a single type
internal class MetricRoutingTestResult : RoutingTestResult
{
    public string? MetricHttpRoute { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}

internal class ActivityRoutingTestResult : RoutingTestResult
{
    public string ActivityDisplayName { get; set; } = string.Empty;

    public string? ActivityHttpRoute { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}
#pragma warning restore SA1402 // File may only contain a single type
