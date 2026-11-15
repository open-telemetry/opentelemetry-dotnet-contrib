// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RouteTests;

public static class RoutingTestCases
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> GetTestCases()
        => [.. LoadTestCases().Select(testCase => testCase.ToString())];

    public static TestCase GetTestCase(string name)
        => LoadTestCases().Single(testCase => string.Equals(testCase.ToString(), name, StringComparison.Ordinal));

    private static List<TestCase> LoadTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<TestCase[]>(
            assembly.GetManifestResourceStream("RoutingTestCases.json")!,
            JsonSerializerOptions);

        var result = new List<TestCase>();
        var dotnetVersion = Environment.Version.Major;

        foreach (var testCase in input!)
        {
            if ((testCase.MinimumDotnetVersion.HasValue && dotnetVersion < testCase.MinimumDotnetVersion.Value) ||
                (testCase.MaximumDotnetVersion.HasValue && dotnetVersion > testCase.MaximumDotnetVersion.Value))
            {
                continue;
            }

            result.Add(testCase);
        }

        return result;
    }
}
