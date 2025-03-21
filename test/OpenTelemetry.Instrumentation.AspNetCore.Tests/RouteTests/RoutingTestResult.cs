// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using RouteTests.TestApplication;

namespace RouteTests;

internal class RoutingTestResult
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public string? IdealHttpRoute { get; set; }

    public string ActivityDisplayName { get; set; } = string.Empty;

    public string? ActivityHttpRoute { get; set; }

    public string? MetricHttpRoute { get; set; }

    public RouteInfo RouteInfo { get; set; } = new();

    [JsonIgnore]
    public TestCase TestCase { get; set; } = new();

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}
