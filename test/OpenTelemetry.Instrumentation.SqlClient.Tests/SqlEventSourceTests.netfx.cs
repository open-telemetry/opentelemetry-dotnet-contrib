// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
public class SqlEventSourceTests
{
    /*
        To run the integration tests, set the OTEL_SQLCONNECTIONSTRING machine-level environment variable to a valid Sql Server connection string.

        To use Docker...
         1) Run: docker run -d --name sql2019 -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Pass@word" -p 5433:1433 mcr.microsoft.com/mssql/server:2019-latest
         2) Set OTEL_SQLCONNECTIONSTRING as: Data Source=127.0.0.1,5433; User ID=sa; Password=Pass@word
     */

    private const string SqlConnectionStringEnvVarName = "OTEL_SQLCONNECTIONSTRING";
    private static readonly string? SqlConnectionString = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(SqlConnectionStringEnvVarName);

    public static IEnumerable<object[]> EventSourceFakeTestCases()
    {
        /* netfx driver can't capture queries, only stored procedure names */
        /* always emit some attribute */
        var bools = new[] { true, false };
        return from eventSourceType in new[] { typeof(FakeBehavingAdoNetSqlEventSource), typeof(FakeBehavingMdsSqlEventSource) }
               from commandType in new[] { CommandType.StoredProcedure, CommandType.Text }
               from isFailure in bools
               from captureText in bools
               from emitOldAttributes in bools
               from emitNewAttributes in bools
               from tracingEnabled in bools
               from metricsEnabled in bools
               where !(commandType == CommandType.Text && captureText)
               where emitOldAttributes && emitNewAttributes
               let commandText = commandType == CommandType.Text
                   ? (!isFailure ? "select 1/1" : "select 1/0")
                   : "sp_who"
               let sqlExceptionNumber = 0
               select new object[]
               {
                   eventSourceType,
                   commandType,
                   commandText,
                   captureText,
                   isFailure,
                   sqlExceptionNumber,
                   emitOldAttributes,
                   emitNewAttributes,
                   tracingEnabled,
                   metricsEnabled,
               };
    }

    [Trait("CategoryName", "SqlIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(SqlConnectionStringEnvVarName)]
    [InlineData(CommandType.Text, "select 1/1", false)]
    [InlineData(CommandType.Text, "select 1/0", false, true)]
    [InlineData(CommandType.StoredProcedure, "sp_who", false)]
    [InlineData(CommandType.StoredProcedure, "sp_who", true)]
    public async Task SuccessfulCommandTest(CommandType commandType, string commandText, bool captureText, bool isFailure = false)
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureText;
            })
            .Build();

        Assert.NotNull(SqlConnectionString);
        using var sqlConnection = new SqlConnection(SqlConnectionString);

        await sqlConnection.OpenAsync();

        var dataSource = sqlConnection.DataSource;

        sqlConnection.ChangeDatabase("master");

#pragma warning disable CA2100
        using var sqlCommand = new SqlCommand(commandText, sqlConnection)
#pragma warning restore CA2100
        {
            CommandType = commandType,
        };

        try
        {
            await sqlCommand.ExecuteNonQueryAsync();
        }
        catch
        {
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(commandText, captureText, isFailure, dataSource, activity);
    }

    [Theory]
    [MemberData(nameof(EventSourceFakeTestCases))]
    public void EventSourceFakeTests(
        Type eventSourceType,
        CommandType commandType,
        string commandText,
        bool captureText,
        bool isFailure = false,
        int sqlExceptionNumber = 0,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false,
        bool tracingEnabled = true,
        bool metricsEnabled = true)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var traceProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (tracingEnabled)
        {
            traceProviderBuilder.AddInMemoryExporter(activities)
                .AddSqlClientInstrumentation(options =>
                {
                    options.SetDbStatementForText = captureText;
                    options.EmitOldAttributes = emitOldAttributes;
                    options.EmitNewAttributes = emitNewAttributes;
                });
        }

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();

        if (metricsEnabled)
        {
            meterProviderBuilder.AddInMemoryExporter(metrics)
                .AddSqlClientInstrumentation();
        }

        using var traceProvider = traceProviderBuilder.Build();
        using var meterProvider = meterProviderBuilder.Build();

        var objectId = Guid.NewGuid().GetHashCode();
        var dataSource = "127.0.0.1\\instanceName,port";

        try
        {
            fakeSqlEventSource.WriteBeginExecuteEvent(objectId, dataSource, "master", commandType == CommandType.StoredProcedure ? commandText : string.Empty);

            // success is stored in the first bit in compositeState 0b001
            var successFlag = !isFailure ? 1 : 0;

            // isSqlException is stored in the second bit in compositeState 0b010
            var isSqlExceptionFlag = sqlExceptionNumber > 0 ? 2 : 0;

            // synchronous state is stored in the third bit in compositeState 0b100
            var synchronousFlag = false ? 4 : 0;

            var compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

            fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState, sqlExceptionNumber);
        }
        finally
        {
            traceProvider.Dispose();
            Assert.True(meterProvider.ForceFlush());
        }

        Activity? activity = null;

        if (tracingEnabled)
        {
            activity = Assert.Single(activities);
            VerifyActivityData(commandText, captureText, isFailure, dataSource, activity, emitOldAttributes, emitNewAttributes);
        }

        var dbClientOperationDurationMetrics = metrics
            .Where(metric => metric.Name == "db.client.operation.duration")
            .ToArray();

        if (metricsEnabled)
        {
            var metric = Assert.Single(dbClientOperationDurationMetrics);
            Assert.NotNull(metric);
            Assert.Equal("s", metric.Unit);
            Assert.Equal(MetricType.Histogram, metric.MetricType);

            var metricPoints = new List<MetricPoint>();
            foreach (var p in metric.GetMetricPoints())
            {
                metricPoints.Add(p);
            }

            var metricPoint = Assert.Single(metricPoints);

            if (activity != null)
            {
                var count = metricPoint.GetHistogramCount();
                var sum = metricPoint.GetHistogramSum();
                Assert.Equal(activity.Duration.TotalSeconds, sum);
            }
        }
        else
        {
            Assert.Empty(dbClientOperationDurationMetrics);
        }
    }

    [Theory]
    [InlineData(typeof(FakeMisbehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeMisbehavingMdsSqlEventSource))]
    public void EventSourceFakeUnknownEventWithNullPayloadTest(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeMisbehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        fakeSqlEventSource.WriteUnknownEventWithNullPayload();

        shutdownSignal.Dispose();

        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(typeof(FakeMisbehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeMisbehavingMdsSqlEventSource))]
    public void EventSourceFakeInvalidPayloadTest(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeMisbehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        fakeSqlEventSource.WriteBeginExecuteEvent("arg1");

        fakeSqlEventSource.WriteEndExecuteEvent("arg1", "arg2", "arg3", "arg4");
        shutdownSignal.Dispose();

        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void DefaultCaptureTextFalse(Type eventSourceType)
    {
        using var fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        var objectId = Guid.NewGuid().GetHashCode();

        const string commandText = "TestCommandTest";
        fakeSqlEventSource.WriteBeginExecuteEvent(objectId, "127.0.0.1", "master", commandText);

        // success is stored in the first bit in compositeState 0b001
        var successFlag = 1;

        // isSqlException is stored in the second bit in compositeState 0b010
        var isSqlExceptionFlag = 2;

        // synchronous state is stored in the third bit in compositeState 0b100
        var synchronousFlag = 4;

        var compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

        fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState, 0);
        shutdownSignal.Dispose();
        Assert.Single(exportedItems);

        var activity = exportedItems[0];

        const bool captureText = false;
        VerifyActivityData(commandText, captureText, false, "127.0.0.1", activity, false);
    }

    private static void VerifyActivityData(
        string commandText,
        bool captureText,
        bool isFailure,
        string dataSource,
        Activity activity,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        if (emitNewAttributes)
        {
            Assert.Equal("instanceName.master", activity.DisplayName);
        }
        else
        {
            Assert.Equal("master", activity.DisplayName);
        }

        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystem));

        var connectionDetails = SqlConnectionDetails.ParseFromDataSource(dataSource);

        if (!string.IsNullOrEmpty(connectionDetails.ServerHostName))
        {
            Assert.Equal(connectionDetails.ServerHostName, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }
        else
        {
            Assert.Equal(connectionDetails.ServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }

        if (emitOldAttributes && !string.IsNullOrEmpty(connectionDetails.InstanceName))
        {
            Assert.Equal(connectionDetails.InstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        }
        else
        {
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        }

        if (connectionDetails.Port.HasValue)
        {
            Assert.Equal(connectionDetails.Port, activity.GetTagValue(SemanticConventions.AttributeServerPort));
        }

        if (emitOldAttributes)
        {
            Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbName));
        }

        if (emitNewAttributes)
        {
            Assert.Equal("instanceName.master", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        }

        if (captureText)
        {
            if (emitOldAttributes)
            {
                Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }

            if (emitNewAttributes)
            {
                Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
            }
        }
        else
        {
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
        }

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);
        }
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    // Helper interface to be able to have single test method for multiple EventSources, want to keep it close to the event sources themselves.
    private interface IFakeBehavingSqlEventSource : IDisposable
#pragma warning restore SA1201 // Elements should appear in the correct order
    {
        void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText);

        void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber);
    }

    private interface IFakeMisbehavingSqlEventSource : IDisposable
    {
        void WriteBeginExecuteEvent(string arg1);

        void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4);

        void WriteUnknownEventWithNullPayload();
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeFriendly")]
    private class FakeBehavingAdoNetSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeFriendly")]
    private class FakeBehavingMdsSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
        }
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeEvil")]
    private class FakeMisbehavingAdoNetSqlEventSource : EventSource, IFakeMisbehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(string arg1)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, arg1);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, arg1, arg2, arg3, arg4);
        }

        [Event(3)]
        public void WriteUnknownEventWithNullPayload()
        {
            object[]? args = null;

            this.WriteEvent(3, args);
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeEvil")]
    private class FakeMisbehavingMdsSqlEventSource : EventSource, IFakeMisbehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(string arg1)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, arg1);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, arg1, arg2, arg3, arg4);
        }

        [Event(3)]
        public void WriteUnknownEventWithNullPayload()
        {
            object[]? args = null;

            this.WriteEvent(3, args);
        }
    }
}
#endif
