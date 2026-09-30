// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Diagnostics.Tracing;
#else
using Microsoft.Data.SqlClient;
#endif
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
public class SqlClientQuerySanitizationTests(ITestOutputHelper output)
{
#if !NETFRAMEWORK
    private const string ConnectionString = "Data Source=tcp:sql-prod.contoso.local,1433;Initial Catalog=Crm;Integrated Security=true;Encrypt=True";
#endif

    private static readonly string[] Customers = ["alice.smith", "bob.jones", "carol.white"];

#if NETFRAMEWORK
    private static int nextObjectId = 1000;
#endif

    private readonly ITestOutputHelper output = output;

    [Fact]
    public void ApostropheInBracketedColumn_DoesNotLeakLiteralsIntoQueryTextSpanNameOrMetricTags()
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        foreach (var customer in Customers)
        {
            Execute(Statement("Nom de l'entreprise", customer));
        }

        // Control: the same statements with an apostrophe-free column name.
        foreach (var customer in Customers)
        {
            Execute(Statement("NomEntreprise", customer));
        }

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        Assert.Equal(Customers.Length * 2, activities.Count);

        for (var i = 0; i < Customers.Length; i++)
        {
            var activity = activities[i];
            var control = activities[Customers.Length + i];
            var queryText = (string)activity.GetTagValue(SemanticConventions.AttributeDbQueryText)!;
            var controlQueryText = (string)control.GetTagValue(SemanticConventions.AttributeDbQueryText)!;

            this.output.WriteLine($"apostrophe: name='{activity.DisplayName}' db.query.text='{queryText}'");
            this.output.WriteLine($"control:    name='{control.DisplayName}' db.query.text='{controlQueryText}'");

            // The literal is sanitized, and the span name is derived from the table name only.
            Assert.DoesNotContain(Customers[i], queryText, StringComparison.Ordinal);
            Assert.Equal("SELECT [a].[Id], [a].[Nom de l'entreprise] FROM [Accounts] AS [a] WHERE [a].[Note] = ?", queryText);
            Assert.Equal("SELECT [Accounts]", activity.DisplayName);
            Assert.Equal("SELECT [Accounts]", activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary));

            Assert.DoesNotContain(Customers[i], controlQueryText, StringComparison.Ordinal);
            Assert.Equal("SELECT [Accounts]", control.DisplayName);
        }

        // Metrics: a single db.client.operation.duration series for every statement.
        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        var summaries = new List<string>();
        foreach (var point in metric.GetMetricPoints())
        {
            foreach (var tag in point.Tags)
            {
                if (tag.Key == SemanticConventions.AttributeDbQuerySummary)
                {
                    summaries.Add((string)tag.Value!);
                }
            }
        }

        this.output.WriteLine($"db.client.operation.duration db.query.summary values: {string.Join(" | ", summaries)}");

        Assert.Equal("SELECT [Accounts]", Assert.Single(summaries));
    }

    private static string Statement(string columnName, string customer)
        => $"SELECT [a].[Id], [a].[{columnName}] FROM [Accounts] AS [a] WHERE [a].[Note] = N'Refund from {customer} approved'";

#if !NETFRAMEWORK
    private static void Execute(string commandText)
    {
        using var connection = new SqlConnection(ConnectionString);
        using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        command.CommandText = commandText;
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);
        var operationId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
            new { OperationId = operationId, Operation = "ExecuteReader", ConnectionId = connectionId, Command = command, Timestamp = Stopwatch.GetTimestamp() });

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
            new { OperationId = operationId, Operation = "ExecuteReader", ConnectionId = connectionId, Command = command, Statistics = (System.Collections.IDictionary?)null, Timestamp = Stopwatch.GetTimestamp() });
    }
#else
    private static void Execute(string commandText)
    {
        using var eventSource = new QuerySanitizationMdsEventSource();
        var objectId = Interlocked.Increment(ref nextObjectId);

        eventSource.WriteBeginExecuteEvent(objectId, "tcp:sql-prod.contoso.local,1433", "Crm", commandText);

        // compositeState 0b001: success.
        eventSource.WriteEndExecuteEvent(objectId, 0b001, 0);
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-QuerySanitizationTests")]
    private sealed class QuerySanitizationMdsEventSource : EventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
            => this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
            => this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
    }
#endif
}
