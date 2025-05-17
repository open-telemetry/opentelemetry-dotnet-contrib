// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Text.Json.Serialization;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

#pragma warning disable SA1402 // File may only contain a single type
public class SqlClientTestCase
{
    public string Name { get; set; } = string.Empty;

    public SqlClientTestCaseInput Input { get; set; } = new();

    public SqlClientTestCaseExpected Expected { get; set; } = new();

    public SqlClientTestCaseOldConventions ExpectedOldConventions { get; set; } = new();
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

    public string DbSystemName { get; set; } = SqlActivitySourceHelper.MicrosoftSqlServerDbSystemName;

    [JsonPropertyName("error.type")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("server.address")]
    public string? ServerAddress { get; set; }

    [JsonPropertyName("server.port")]
    public int? ServerPort { get; set; }

    public string SpanName { get; set; } = string.Empty;
}

public class SqlClientTestCaseOldConventions
{
    [JsonPropertyName("db.mssql.instance_name")]
    public string? DbMsSqlInstanceName { get; set; }

    [JsonPropertyName("db.name")]
    public string? DbName { get; set; }

    public string DbSystem { get; set; } = SqlActivitySourceHelper.MicrosoftSqlServerDbSystem;

    [JsonPropertyName("db.statement")]
    public string? DbStatement { get; set; }

    public string SpanName { get; set; } = string.Empty;
}
#pragma warning restore SA1402 // File may only contain a single type
