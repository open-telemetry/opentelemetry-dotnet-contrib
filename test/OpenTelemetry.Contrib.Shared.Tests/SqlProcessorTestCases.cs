// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.Tests;

public static class SqlProcessorTestCases
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly HashSet<string> DbSystemTestCasesToExecute = ["other_sql"];

    public static TheoryData<TestCase> GetSemanticConventionsTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<TestCase[]>(
            assembly.GetManifestResourceStream("SqlProcessorTestCases.json")!,
            JsonSerializerOptions)!;

        var data = new TheoryData<TestCase>();

        if (input is not null)
        {
            foreach (var testCase in input)
            {
                if (DbSystemTestCasesToExecute.Contains(testCase.Input.DbSystemName))
                {
                    data.Add(testCase);
                }
            }
        }

        return data;
    }

#pragma warning disable CA1034 // Nested types should not be visible
    public class TestCase : IXunitSerializable
    {
        public string Name { get; set; } = string.Empty;

        public TestCaseInput Input { get; set; } = new();

        public TestCaseExpected Expected { get; set; } = new();

        public void Deserialize(IXunitSerializationInfo info)
        {
            var json = info.GetValue<string>("json") ?? string.Empty;

            var proxy = JsonSerializer.Deserialize<TestCase>(json, JsonSerializerOptions);

            if (proxy is not null)
            {
                this.Name = proxy.Name;
                this.Input = proxy.Input;
                this.Expected = proxy.Expected;
            }
            else
            {
                throw new InvalidOperationException("Deserialized TestCase is null.");
            }
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            var json = JsonSerializer.Serialize(this, JsonSerializerOptions);
            info.AddValue("json", json);
        }

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
