// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;

namespace OpenTelemetry.Instrumentation.Http.Tests;

internal static class HttpTestData
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static TheoryData<string> TestData()
        => [.. LoadTestCases().Select(testCase => testCase.Name)];

    public static HttpOutTestCase GetTestCase(string name)
        => LoadTestCases().Single(testCase => string.Equals(testCase.Name, name, StringComparison.Ordinal));

    public static string NormalizeValues(string value, string host, int port)
        => value
            .Replace("{host}", host)
            .Replace("{port}", port.ToString())
            .Replace("{flavor}", "1.1");

    private static HttpOutTestCase[] LoadTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
#pragma warning disable IDE0370 // Suppression is unnecessary
        var input = JsonSerializer.Deserialize<HttpOutTestCase[]>(
            assembly.GetManifestResourceStream("OpenTelemetry.Instrumentation.Http.Tests.http-out-test-cases.json")!,
            JsonSerializerOptions);
#pragma warning restore IDE0370 // Suppression is unnecessary

        return input ?? [];
    }
}
