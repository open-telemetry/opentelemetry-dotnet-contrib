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

    public static IEnumerable<object[]> GetTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<TestCase[]>(
            assembly.GetManifestResourceStream("SqlProcessorTestCases.json")!,
            JsonSerializerOptions)!;

        foreach (var testCase in input)
        {
            yield return new object[] { testCase };
        }
    }

#pragma warning disable CA1034 // Nested types should not be visible
    public class TestCase
#pragma warning restore CA1034
    {
        public string Name { get; set; } = string.Empty;

        public string Sql { get; set; } = string.Empty;

        public string Sanitized { get; set; } = string.Empty;

        public IEnumerable<string> Dialects { get; set; } = [];

        public override string ToString()
        {
            return this.Name;
        }
    }
}
