// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
#if NET
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
#endif
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
public class SqlClientTraceInstrumentationOptionsTests
{
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master;Encrypt=True;TrustServerCertificate=True";

    [Fact]
    public void SqlClient_NamedOptions()
    {
        var defaultExporterOptionsConfigureOptionsInvocations = 0;
        var namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<SqlClientTraceInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<SqlClientTraceInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddSqlClientInstrumentation()
            .AddSqlClientInstrumentation("Instrumentation2", configureSqlClientTraceInstrumentationOptions: null)
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Fact]
    public void DbQueryTextCollected()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        var commandText = "select * from sys.databases";
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, commandText, false, SqlClientLibrary.SystemDataSqlClient);
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, commandText, false, SqlClientLibrary.MicrosoftDataSqlClient);

        tracerProvider.ForceFlush();
        Assert.Equal(2, activities.Count);

        Assert.Equal(commandText, activities[0].GetTagValue(SemanticConventions.AttributeDbQueryText));
        Assert.Equal(commandText, activities[1].GetTagValue(SemanticConventions.AttributeDbQueryText));
    }

#if !NETFRAMEWORK
    [Fact]
    public void MultipleTracerProviders_DoNotLeakFilterConfiguration()
    {
        var filteredActivities = new List<Activity>();
        var defaultActivities = new List<Activity>();

        using (var filteredProvider = Sdk.CreateTracerProviderBuilder()
                   .AddSqlClientInstrumentation(options => options.Filter = _ => false)
                   .AddInMemoryExporter(filteredActivities)
                   .Build())
        using (var defaultProvider = Sdk.CreateTracerProviderBuilder()
                   .AddSqlClientInstrumentation()
                   .AddInMemoryExporter(defaultActivities)
                   .Build())
        {
            MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, "SELECT * FROM Foo", false, SqlClientLibrary.MicrosoftDataSqlClient);

            filteredProvider.ForceFlush();
            defaultProvider.ForceFlush();
        }

        Assert.Empty(filteredActivities);
        Assert.Empty(defaultActivities);
    }

    [Fact]
    public void MultipleTracerProviders_DoNotLeakEnrichmentConfiguration()
    {
        var defaultActivities = new List<Activity>();
        var enrichedActivities = new List<Activity>();

        using (var defaultProvider = Sdk.CreateTracerProviderBuilder()
                   .AddSqlClientInstrumentation()
                   .AddInMemoryExporter(defaultActivities)
                   .Build())
        using (var enrichedProvider = Sdk.CreateTracerProviderBuilder()
                   .AddSqlClientInstrumentation(options => options.EnrichWithSqlCommand = ActivityEnrichment)
                   .AddInMemoryExporter(enrichedActivities)
                   .Build())
        {
            MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, "SELECT * FROM Foo", false, SqlClientLibrary.MicrosoftDataSqlClient);

            defaultProvider.ForceFlush();
            enrichedProvider.ForceFlush();
        }

        var defaultActivity = Assert.Single(defaultActivities);
        var enrichedActivity = Assert.Single(enrichedActivities);

        Assert.DoesNotContain(defaultActivity.Tags, tag => tag.Key == "enriched");
        Assert.DoesNotContain(enrichedActivity.Tags, tag => tag.Key == "enriched");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExceptionCapturedWhenRecordExceptionEnabled(bool recordException)
    {
        var activities = new List<Activity>();

        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordException = recordException;
            })
            .AddInMemoryExporter(activities)
            .Build();

        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", true, SqlClientLibrary.SystemDataSqlClient);
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", true, SqlClientLibrary.MicrosoftDataSqlClient);

        traceProvider.ForceFlush();

        Assert.Equal(2, activities.Count);

        Assert.Equal(ActivityStatusCode.Error, activities[0].Status);
        Assert.Equal(ActivityStatusCode.Error, activities[1].Status);
        Assert.NotNull(activities[0].StatusDescription);
        Assert.NotNull(activities[1].StatusDescription);

        if (recordException)
        {
            var events0 = activities[0].Events.ToList();
            var events1 = activities[1].Events.ToList();
            Assert.Single(events0);
            Assert.Single(events1);
            Assert.Equal(SemanticConventions.AttributeExceptionEventName, events0[0].Name);
            Assert.Equal(SemanticConventions.AttributeExceptionEventName, events1[0].Name);
        }
        else
        {
            Assert.Empty(activities[0].Events);
            Assert.Empty(activities[1].Events);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ShouldEnrichWhenEnabled(bool shouldEnrich, bool error)
    {
        var activities = new List<Activity>();

        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(
            (opt) =>
            {
                if (shouldEnrich)
                {
                    opt.EnrichWithSqlCommand = ActivityEnrichment;
                }
            })
            .AddInMemoryExporter(activities)
            .Build();

        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, "SELECT * FROM Foo", error, SqlClientLibrary.SystemDataSqlClient);
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, "SELECT * FROM Foo", error, SqlClientLibrary.MicrosoftDataSqlClient);

        Assert.Equal(2, activities.Count);
        if (shouldEnrich)
        {
            Assert.Contains("enriched", activities[0].Tags.Select(x => x.Key));
            Assert.Contains("enriched", activities[1].Tags.Select(x => x.Key));
            Assert.Equal("yes", activities[0].Tags.FirstOrDefault(tag => tag.Key == "enriched").Value);
            Assert.Equal("yes", activities[1].Tags.FirstOrDefault(tag => tag.Key == "enriched").Value);
        }
        else
        {
            Assert.DoesNotContain(activities[0].Tags, tag => tag.Key == "enriched");
            Assert.DoesNotContain(activities[1].Tags, tag => tag.Key == "enriched");
        }
    }

    [Fact]
    public void ShouldCollectTelemetryWhenFilterEvaluatesToTrue()
    {
        var activities = this.RunCommandWithFilter(
            "select 2",
            cmd =>
            {
                return cmd is not SqlCommand command || command.CommandText == "select 2";
            });

        Assert.Single(activities);
        Assert.True(activities[0].IsAllDataRequested);
        Assert.True(activities[0].ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));
    }

    [Fact]
    public void ShouldNotCollectTelemetryWhenFilterEvaluatesToFalse()
    {
        var activities = this.RunCommandWithFilter(
            "select 1",
            cmd =>
            {
                return cmd is not SqlCommand command || command.CommandText == "select 2";
            });

        Assert.Empty(activities);
    }

    [Fact]
    public void ShouldNotCollectTelemetryAndShouldNotPropagateExceptionWhenFilterThrowsException()
    {
        var activities = this.RunCommandWithFilter(
            "select 1",
            cmd => throw new InvalidOperationException("foobar"));

        Assert.Empty(activities);
    }

    [Fact]
    public void ShouldNotEmitDatabaseQueryParametersByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.False(options.SetDbQueryParameters);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void ShouldAssignSetDatabaseQueryParametersFromEnvironmentVariable(string value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_TRACE_DB_QUERY_PARAMETERS"] = value })
            .Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.Equal(expected, options.SetDbQueryParameters);
    }

    [Fact]
    public void ShouldNotRecordReturnedRowsByDefault()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.False(options.RecordReturnedRows);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("invalid", false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void ShouldAssignRecordReturnedRowsFromEnvironmentVariable(string value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_DOTNET_EXPERIMENTAL_SQLCLIENT_ENABLE_RECORD_RETURNED_ROWS"] = value })
            .Build();
        var options = new SqlClientTraceInstrumentationOptions(configuration);
        Assert.Equal(expected, options.RecordReturnedRows);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordReturnedRowsCollected(bool recordReturnedRows)
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordReturnedRows = recordReturnedRows;
            })
            .AddInMemoryExporter(activities)
            .Build();

        // A query reports rows via SelectRows, a data manipulation command via IduRows,
        // and whichever statistic is non-zero should populate db.response.returned_rows.
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, "select * from Foo", false, SqlClientLibrary.MicrosoftDataSqlClient, selectRows: 20);
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, "update Foo set Bar = 1", false, SqlClientLibrary.MicrosoftDataSqlClient, iduRows: 10);

        tracerProvider.ForceFlush();
        Assert.Equal(2, activities.Count);

        if (recordReturnedRows)
        {
            Assert.Equal(20L, activities[0].GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
            Assert.Equal(10L, activities[1].GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
        }
        else
        {
            Assert.Null(activities[0].GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
            Assert.Null(activities[1].GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
        }
    }

    [Fact]
    public void RecordReturnedRowsFallsBackToSelectRowsWhenIduRowsAbsent()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordReturnedRows = true;
            })
            .AddInMemoryExporter(activities)
            .Build();

        var statsWithoutIduRows = new Dictionary<string, object>
        {
            ["SelectRows"] = 15L,
        };

        MockCommandExecutor.ExecuteCommand(
            TestConnectionString,
            CommandType.Text,
            "select * from Foo",
            false,
            SqlClientLibrary.MicrosoftDataSqlClient,
            statsWithoutIduRows);

        tracerProvider.ForceFlush();

        var activity = Assert.Single(activities);

        Assert.Equal(15L, activity.GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
    }

    [Fact]
    public void RecordReturnedRowsNotRecordedWhenRowStatisticsAbsent()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordReturnedRows = true;
            })
            .AddInMemoryExporter(activities)
            .Build();

        // Statistics that contain neither IduRows nor SelectRows should not populate the tag.
        var statisticsWithoutRowCounts = new Dictionary<string, object>
        {
            ["BuffersReceived"] = 1L,
        };

        MockCommandExecutor.ExecuteCommand(
            TestConnectionString,
            CommandType.Text,
            "select * from Foo",
            false,
            SqlClientLibrary.MicrosoftDataSqlClient,
            statisticsWithoutRowCounts);

        tracerProvider.ForceFlush();

        var activity = Assert.Single(activities);

        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
    }

    [Fact]
    public void RecordReturnedRowsWhenCommandHasNoConnection()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordReturnedRows = true;
            })
            .AddInMemoryExporter(activities)
            .Build();

        // A command with no connection cannot report a baseline, so the reported value
        // is simply the SelectRows reported on the after-command statistics.
        var command = new FakeDbCommand
        {
            CommandType = CommandType.Text,
            CommandText = "select * from Foo",
            Connection = null,
        };

        var statistics = new Dictionary<string, object>
        {
            ["SelectRows"] = 7L,
        };

        MockCommandExecutor.ExecuteCommand(command, SqlClientLibrary.MicrosoftDataSqlClient, statistics);

        tracerProvider.ForceFlush();

        var activity = Assert.Single(activities);

        Assert.Equal(7L, activity.GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
    }

    [Fact]
    public void RecordReturnedRowsWhenConnectionHasNoStatistics()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordReturnedRows = true;
            })
            .AddInMemoryExporter(activities)
            .Build();

        // The connection type does not expose RetrieveStatistics(), so no baseline can be
        // captured and the reported value falls back to the after-command statistics.
        var command = new FakeDbCommand
        {
            CommandType = CommandType.Text,
            CommandText = "select * from Foo",
            Connection = new FakeDbConnection(),
        };

        var statistics = new Dictionary<string, object>
        {
            ["SelectRows"] = 9L,
        };

        MockCommandExecutor.ExecuteCommand(command, SqlClientLibrary.MicrosoftDataSqlClient, statistics);

        tracerProvider.ForceFlush();

        var activity = Assert.Single(activities);

        Assert.Equal(9L, activity.GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
    }

    [Fact]
    public void RecordReturnedRowsWhenConnectionStatisticsThrow()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.RecordReturnedRows = true;
            })
            .AddInMemoryExporter(activities)
            .Build();

        // RetrieveStatistics() throws, so the baseline defaults to zero and the reported
        // value falls back to the after-command statistics.
        var command = new FakeDbCommand
        {
            CommandType = CommandType.Text,
            CommandText = "update Foo set Bar = 1",
            Connection = new ThrowingStatisticsDbConnection(),
        };

        var statistics = new Dictionary<string, object>
        {
            ["IduRows"] = 5L,
        };

        MockCommandExecutor.ExecuteCommand(command, SqlClientLibrary.MicrosoftDataSqlClient, statistics);

        tracerProvider.ForceFlush();

        var activity = Assert.Single(activities);

        Assert.Equal(5L, activity.GetTagValue(SemanticConventions.AttributeDbResponseReturnedRows));
    }

    private static void ActivityEnrichment(Activity activity, object obj)
    {
        activity.SetTag("enriched", "yes");
        Assert.IsType<SqlCommand>(obj);
    }

    private Activity[] RunCommandWithFilter(
        string commandText,
        Func<object, bool> filter)
    {
        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
           .AddSqlClientInstrumentation(
               options =>
               {
                   options.Filter = filter;
               })
           .AddInMemoryExporter(activities)
           .Build())
        {
            MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.Text, commandText, false, SqlClientLibrary.MicrosoftDataSqlClient);
        }

        return [.. activities];
    }

    // A connection whose RetrieveStatistics() method throws, exercising the code path where
    // resolving the connection statistics fails and the baseline row counts default to zero.
    private sealed class ThrowingStatisticsDbConnection : FakeDbConnection
    {
        public IDictionary RetrieveStatistics() => throw new InvalidOperationException("Statistics are not available.");
    }
#endif
}
