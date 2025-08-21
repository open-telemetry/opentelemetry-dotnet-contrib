// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTelemetry.Instrumentation.Tests;

public static class SqlProcessorTestCases
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly HashSet<string> DbSystemTestCasesToExecute = ["other_sql"];

    public static IEnumerable<object[]> GetSemanticConventionsTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<TestCase[]>(
            assembly.GetManifestResourceStream("SqlProcessorTestCases.json")!,
            JsonSerializerOptions)!;

        if (input is not null)
        {
            foreach (var testCase in input)
            {
                if (DbSystemTestCasesToExecute.Contains(testCase.Input.DbSystemName))
                {
                    yield return new object[] { testCase };
                }
            }
        }
    }

#pragma warning disable CA1034 // Nested types should not be visible
    public class TestCase
    {
        public string Name { get; set; } = string.Empty;

        public TestCaseInput Input { get; set; } = new();

        public TestCaseExpected Expected { get; set; } = new();

        public override string ToString() => this.Name;
    }

    public class TestCaseInput
    {
        [JsonPropertyName("db.system.name")]
        public string DbSystemName { get; set; } = string.Empty;

        public string Query { get; set; } = string.Empty;
    }

    public class TestCaseExpected
    {
        [JsonPropertyName("db.query.text")]
#pragma warning disable CA1819 // Properties should not return arrays
        public string[] SanitizedQueryText { get; set; } = [];
#pragma warning restore CA1819

        [JsonPropertyName("db.query.summary")]
        public string? Summary { get; set; }
    }
#pragma warning restore CA1034
}
