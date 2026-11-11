// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

#pragma warning disable SA1402 // File may only contain a single type
public static class SqlClientTestCases
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> GetTestCaseNames()
        => [.. LoadTestCases().Select(testCase => testCase.Name)];

    public static SqlClientTestCase GetTestCase(string name)
        => LoadTestCases().Single(testCase => string.Equals(testCase.Name, name, StringComparison.Ordinal));

    private static SqlClientTestCase[] LoadTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var input = JsonSerializer.Deserialize<SqlClientTestCase[]>(
            assembly.GetManifestResourceStream("SqlClientTestCases.json")!,
            JsonSerializerOptions);

#if NETFRAMEWORK
        foreach (var testCase in input!)
        {
            testCase.Expected.DbOperationName = testCase.ExpectedNetFramework.DbOperationName;
            testCase.Expected.DbQuerySummary = testCase.ExpectedNetFramework.DbQuerySummary;
            testCase.Expected.DbQueryText = testCase.ExpectedNetFramework.DbQueryText;
            testCase.Expected.DbStoredProcedureName = testCase.ExpectedNetFramework.DbStoredProcedureName;
            testCase.Expected.SpanName = testCase.ExpectedNetFramework.SpanName;
        }
#endif

        return input!;
    }
}

public class SqlClientTestCase
{
    public string Name { get; set; } = string.Empty;

    public SqlClientTestCaseInput Input { get; set; } = new();

    public SqlClientTestCaseExpected Expected { get; set; } = new();

    public SqlClientTestCaseExpected ExpectedNetFramework { get; set; } = new();
}

public class SqlClientTestCaseInput
{
    public string ConnectionString { get; set; } = string.Empty;

    public CommandType CommandType { get; set; }

    public string CommandText { get; set; } = string.Empty;
}

public class SqlClientTestCaseExpected
{
    [JsonPropertyName("db.collection.name")]
    public string DbCollectionName { get; set; } = string.Empty;

    [JsonPropertyName("db.namespace")]
    public string DbNamespace { get; set; } = string.Empty;

    [JsonPropertyName("db.operation.batch.size")]
    public int? DbOperationBatchSize { get; set; }

    [JsonPropertyName("db.operation.name")]
    public string? DbOperationName { get; set; }

    [JsonPropertyName("db.query.summary")]
    public string? DbQuerySummary { get; set; }

    [JsonPropertyName("db.query.text")]
    public string? DbQueryText { get; set; }

    [JsonPropertyName("db.response.status_code")]
    public string? DbResponseStatusCode { get; set; }

    [JsonPropertyName("db.stored_procedure.name")]
    public string? DbStoredProcedureName { get; set; }

    public string DbSystemName { get; set; } = SqlTelemetryHelper.MicrosoftSqlServerDbSystemName;

    [JsonPropertyName("error.type")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("server.address")]
    public string? ServerAddress { get; set; }

    [JsonPropertyName("server.port")]
    public int? ServerPort { get; set; }

    public string SpanName { get; set; } = string.Empty;
}
#pragma warning restore SA1402 // File may only contain a single type
