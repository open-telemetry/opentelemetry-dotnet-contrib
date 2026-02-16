// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace RouteTests;

public static class RoutingTestCases
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<TestCase> GetTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<TestCase[]>(
            assembly.GetManifestResourceStream("RoutingTestCases.json")!,
            JsonSerializerOptions);
        return GetArgumentsFromTestCaseObject(input!);
    }

    private static TheoryData<TestCase> GetArgumentsFromTestCaseObject(IEnumerable<TestCase> input)
    {
        var result = new TheoryData<TestCase>();
        var dotnetVersion = Environment.Version.Major;

        foreach (var testCase in input)
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
