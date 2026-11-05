// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Collections;
#endif
using System.Data;
using System.Diagnostics;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public enum SqlClientLibrary
{
    SystemDataSqlClient,
    MicrosoftDataSqlClient,
}

[Collection("SqlClient")]
public class SqlClientTests
{
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=main;Encrypt=True;TrustServerCertificate=True";

    private enum SamplingMode
    {
        /// <summary>
        /// Commands are traced and sampled.
        /// </summary>
        Sampled,

        /// <summary>
        /// Commands are traced but not sampled, so an activity which is not recorded is created for them.
        /// </summary>
        NotRecorded,

        /// <summary>
        /// Commands are traced but not sampled, and their parent is not sampled, so no activity is created for them.
        /// </summary>
        NotSampled,

        /// <summary>
        /// Commands are not traced.
        /// </summary>
        MetricsOnly,
    }

    public static IEnumerable<object[]> TestData => SqlClientTestCases.GetTestCases();

    [Fact]
    public void SqlClient_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddSqlClientInstrumentation());
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestMicrosoftDataSqlClient(SqlClientTestCase testCase)
        => this.RunSqlClientTestCase(testCase, SqlClientLibrary.MicrosoftDataSqlClient);

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSystemDataSqlClient(SqlClientTestCase testCase)
        => this.RunSqlClientTestCase(testCase, SqlClientLibrary.SystemDataSqlClient);

    [Theory]
    [InlineData("localhost", "localhost", null, null)]
    [InlineData("127.0.0.1,1433", null, "127.0.0.1", null)]
    [InlineData("127.0.0.1,1434", null, "127.0.0.1", 1434)]
    [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", 1818)]
    public void SqlClientAddsConnectionLevelAttributes(
        string dataSource,
        string? expectedServerHostName,
        string? expectedServerIpAddress,
        int? expectedPort)
    {
        var tags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource, databaseName: null, out var _);
        Assert.Equal(expectedServerHostName ?? expectedServerIpAddress, tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeServerAddress).Value);
        Assert.Equal(expectedPort, tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeServerPort).Value);
    }

    [Theory]
    [InlineData("localhost", "main")]
    [InlineData("localhost\\instanceName", "instanceName.main")]
    public void SqlClientAddsDatabaseNamespaceAttribute(string dataSource, string expectedDbNamespace)
    {
        var tags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource, databaseName: "main", out var activityName);

        Assert.Equal(expectedDbNamespace, activityName);
        Assert.Equal(expectedDbNamespace, tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeDbNamespace).Value);
    }

    [Fact]
    public void SqlClientAddsDatabaseNamespaceAttributeWhenThereIsNoDataSource()
    {
        var tags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource: null, databaseName: "main", out var activityName);

        Assert.Equal("main", activityName);
        Assert.Equal("main", tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeDbNamespace).Value);
        Assert.DoesNotContain(tags, x => x.Key == SemanticConventions.AttributeServerAddress);
    }

    [Fact]
    public void SqlClientDoesNotAddDatabaseNamespaceAttributeWhenThereIsNoDataSourceOrDatabase()
    {
        var tags = SqlTelemetryHelper.GetTagListFromConnectionInfo(dataSource: null, databaseName: null, out var activityName);

        Assert.Equal(SqlTelemetryHelper.MicrosoftSqlServerDbSystemName, activityName);
        Assert.DoesNotContain(tags, x => x.Key == SemanticConventions.AttributeDbNamespace);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void SpansAndMetricsGeneratedOnlyWhenEnabled(bool tracesEnabled, bool metricsEnabled)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();
        if (tracesEnabled)
        {
            tracerProviderBuilder
                .AddSqlClientInstrumentation(options =>
                {
#if NET
                    options.RecordException = true;
#endif
                })
                .AddInMemoryExporter(activities);
        }

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();
        if (metricsEnabled)
        {
            meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                .AddSqlClientInstrumentation()
                .AddInMemoryExporter(metrics);
        }

        using var tracerProvider = tracerProviderBuilder.Build();
        using var meterProvider = meterProviderBuilder.Build();

        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", false, SqlClientLibrary.SystemDataSqlClient);
        MockCommandExecutor.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", false, SqlClientLibrary.MicrosoftDataSqlClient);

        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        Assert.Equal(tracesEnabled ? 2 : 0, activities.Count);

        if (metricsEnabled)
        {
            var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
            var metricPoints = new List<MetricPoint>();
            foreach (var p in metric.GetMetricPoints())
            {
                metricPoints.Add(p);
            }

            var metricPoint = Assert.Single(metricPoints);
            var measurementCount = metricPoint.GetHistogramCount();
            Assert.Equal(2, measurementCount);
        }
        else
        {
            Assert.Empty(metrics);
        }
    }

#if !NETFRAMEWORK
    [Fact]
    public void MetricDurationUsesPerCommandStartTimestampForReEntrantCommands()
    {
        var metrics = new List<Metric>();

        // Metrics only, so no activity is created for either command and the durations must come
        // from the start timestamps captured by the before events.
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        Assert.False(SqlTelemetryHelper.ActivitySource.HasListeners());

        // Stored procedures are used so that each command contributes a distinct metric point via
        // db.stored_procedure.name, letting the two durations be told apart.
        var outerCommand = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_Outer",
            Connection = new FakeDbConnection(),
        };

        var innerCommand = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_Inner",
            Connection = new FakeDbConnection(),
        };

        // The inner command starts, and both commands finish, only after the outer command has
        // already been running for the delay below. A single shared start timestamp would be
        // overwritten by the inner command's before event, so the outer command would report
        // roughly the inner command's (near instant) duration instead of its own.
        var delay = TimeSpan.FromMilliseconds(500);

        MockCommandExecutor.ExecuteNestedCommands(
            SqlClientLibrary.MicrosoftDataSqlClient,
            outerCommand,
            null,
            innerCommand,
            null,
            afterOuterCommandStarted: () => Thread.Sleep(delay));

        meterProvider.ForceFlush();

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");

        var metricPoints = new List<MetricPoint>();
        foreach (var p in metric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        Assert.Equal(2, metricPoints.Count);

        var outerDuration = GetDurationForStoredProcedure(metricPoints, outerCommand.CommandText);
        var innerDuration = GetDurationForStoredProcedure(metricPoints, innerCommand.CommandText);

        // Half the delay is used as the threshold to keep the assertions robust on slow machines
        // while still being far away from the near instant duration the shared slot would report.
        Assert.True(outerDuration >= delay.TotalSeconds / 2, $"Outer duration {outerDuration}s should span the {delay.TotalSeconds}s the outer command was running.");
        Assert.True(innerDuration < delay.TotalSeconds / 2, $"Inner duration {innerDuration}s should not include the {delay.TotalSeconds}s that elapsed before the inner command started.");

        static double GetDurationForStoredProcedure(List<MetricPoint> metricPoints, string storedProcedureName)
        {
            foreach (var metricPoint in metricPoints)
            {
                foreach (var tag in metricPoint.Tags)
                {
                    if (tag.Key == SemanticConventions.AttributeDbStoredProcedureName &&
                        (string?)tag.Value == storedProcedureName)
                    {
                        Assert.Equal(1, metricPoint.GetHistogramCount());
                        return metricPoint.GetHistogramSum();
                    }
                }
            }

            Assert.Fail($"No metric point was recorded for stored procedure '{storedProcedureName}'.");
            return 0d;
        }
    }

    [Fact]
    public void PendingStartTimestampIsReleasedWhenInstrumentationIsDisabledMidCommand()
    {
        var listener = new SqlClientDiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        var command = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_GetOrders",
            Connection = new FakeDbConnection(),
        };

        var operationId = Guid.NewGuid();

        using (Sdk.CreateMeterProviderBuilder().AddSqlClientInstrumentation().Build())
        {
            Assert.False(SqlTelemetryHelper.ActivitySource.HasListeners());

            listener.OnEventWritten(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                new
                {
                    OperationId = operationId,
                    Command = command,
                    Timestamp = (long?)1000000L,
                });

            Assert.Equal(1, listener.PendingCommandCount);
        }

        listener.OnEventWritten(
            SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
            new
            {
                OperationId = operationId,
                Command = command,
                Statistics = (IDictionary?)null,
                Timestamp = 2000000L,
            });

        Assert.Equal(0, listener.PendingCommandCount);
    }

    [Fact]
    public void MetricDurationNotRecordedWhenCommandHasNoStartTimestamp()
    {
        var metrics = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        MockCommandExecutor.WriteCommandAfterWithoutBefore(
            SqlClientLibrary.MicrosoftDataSqlClient,
            new FakeDbCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "SP_GetOrders",
                Connection = new FakeDbConnection(),
            });

        meterProvider.ForceFlush();

        Assert.Empty(metrics);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CallerActivityIsRestoredWhenAsynchronousCommandCompletesOnAnotherExecutionContext(bool hasCallerActivity, bool error)
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        // The caller is sampled, so the activities of the commands are recorded.
        using var callerActivity = hasCallerActivity
            ? new Activity("caller").SetIdFormat(ActivityIdFormat.W3C).Start()
            : null;

        callerActivity?.ActivityTraceFlags = ActivityTraceFlags.Recorded;

        Assert.Equal(callerActivity, Activity.Current);

        const int Commands = 3;

        for (var i = 0; i < Commands; i++)
        {
            var command = new FakeDbCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "SP_GetOrders",
                Connection = new FakeDbConnection(),
            };

            var operationId = Guid.NewGuid();

            // SqlClient writes the event which starts an asynchronous command on the execution context of the
            // caller, and the event which completes it from a continuation, on a copy of that context.
            listener.Write(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                new
                {
                    OperationId = operationId,
                    Operation = "InternalExecuteNonQueryAsync",
                    Command = command,
                    Timestamp = (long?)1000000L,
                });

            Assert.Equal(callerActivity, Activity.Current);

            void WriteCompletionEvent()
            {
                if (error)
                {
                    listener.Write(
                        SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError,
                        new
                        {
                            OperationId = operationId,
                            Operation = "InternalExecuteNonQueryAsync",
                            Command = command,
                            Exception = new InvalidOperationException("Boom!"),
                            Timestamp = 2000000L,
                        });
                }
                else
                {
                    listener.Write(
                        SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
                        new
                        {
                            OperationId = operationId,
                            Operation = "CleanupAfterExecuteNonQueryAsync",
                            Command = command,
                            Statistics = (IDictionary?)null,
                            Timestamp = 2000000L,
                        });
                }
            }

            await Task.Run(WriteCompletionEvent, TestContext.Current.CancellationToken);

            Assert.Equal(callerActivity, Activity.Current);
        }

        tracerProvider.ForceFlush();

        Assert.Equal(Commands, activities.Count);
        Assert.All(activities, x => Assert.True(x.IsStopped));
        Assert.All(activities, x => Assert.Equal(callerActivity?.SpanId ?? default, x.ParentSpanId));
        Assert.All(activities, x => Assert.Equal(error ? ActivityStatusCode.Error : ActivityStatusCode.Unset, x.Status));
    }

    [Fact]
    public void CallerActivityIsRestoredWhenCommandNeverCompletes()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        // A command executed with a token which is already cancelled writes the event which starts it, but never
        // the event which completes it.
        for (var i = 0; i < 3; i++)
        {
            listener.Write(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                new
                {
                    OperationId = Guid.NewGuid(),
                    Operation = "InternalExecuteNonQueryAsync",
                    Command = new FakeDbCommand
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandText = "SP_GetOrders",
                        Connection = new FakeDbConnection(),
                    },
                    Timestamp = (long?)1000000L,
                });

            Assert.Null(Activity.Current);
        }

        tracerProvider.ForceFlush();

        Assert.Empty(activities);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentActivityOfCompletionIsNotChangedWhenCommandCompletes(bool error)
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        var command = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_GetOrders",
            Connection = new FakeDbConnection(),
        };

        var operationId = Guid.NewGuid();

        using var caller = new Activity("caller").SetIdFormat(ActivityIdFormat.W3C).Start();
        caller.ActivityTraceFlags = ActivityTraceFlags.Recorded;

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
            new
            {
                OperationId = operationId,
                Operation = "InternalExecuteNonQueryAsync",
                Command = command,
                Timestamp = (long?)1000000L,
            });

        // The command completes on an execution context which has a different current activity to the caller.
        using var other = new Activity("other").SetIdFormat(ActivityIdFormat.W3C).Start();

        Assert.Equal(other, Activity.Current);

        if (error)
        {
            listener.Write(
                SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError,
                new
                {
                    OperationId = operationId,
                    Operation = "InternalExecuteNonQueryAsync",
                    Command = command,
                    Exception = new InvalidOperationException("Boom!"),
                    Timestamp = 2000000L,
                });
        }
        else
        {
            listener.Write(
                SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
                new
                {
                    OperationId = operationId,
                    Operation = "CleanupAfterExecuteNonQueryAsync",
                    Command = command,
                    Statistics = (IDictionary?)null,
                    Timestamp = 2000000L,
                });
        }

        Assert.Equal(other, Activity.Current);

        tracerProvider.ForceFlush();

        var activity = Assert.Single(activities);
        Assert.True(activity.IsStopped);
        Assert.Equal(caller.SpanId, activity.ParentSpanId);
    }

    [Fact]
    public void ActivityOfCommandWhichNeverCompletedIsNotUsedWhenItIsExecutedAgain()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        var command = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_GetOrders",
            Connection = new FakeDbConnection(),
        };

        // The first execution is cancelled before it starts, so it never completes.
        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
            new
            {
                OperationId = Guid.NewGuid(),
                Operation = "InternalExecuteNonQueryAsync",
                Command = command,
                Timestamp = (long?)1000000L,
            });

        // The same command is then executed again under a parent which is not sampled, so no activity is created.
        using var parent = new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start();

        var operationId = Guid.NewGuid();

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
            new
            {
                OperationId = operationId,
                Operation = "InternalExecuteNonQueryAsync",
                Command = command,
                Timestamp = (long?)1000000L,
            });

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
            new
            {
                OperationId = operationId,
                Operation = "CleanupAfterExecuteNonQueryAsync",
                Command = command,
                Statistics = (IDictionary?)null,
                Timestamp = 2000000L,
            });

        tracerProvider.ForceFlush();

        Assert.Empty(activities);
        Assert.Equal(parent, Activity.Current);
    }

    [Fact]
    public void ActivityOfCommandWhichNeverCompletedIsNotUsedWhenItIsExecutedWhileInstrumentationIsDisabled()
    {
        var activities = new List<Activity>();

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        var command = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_GetOrders",
            Connection = new FakeDbConnection(),
        };

        void WriteBefore(Guid operationId) =>
            listener.Write(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                new
                {
                    OperationId = operationId,
                    Operation = "InternalExecuteNonQueryAsync",
                    Command = command,
                    Timestamp = (long?)1000000L,
                });

        using (Sdk.CreateTracerProviderBuilder().AddSqlClientInstrumentation().Build())
        {
            // The first execution is cancelled before it starts, so it never completes.
            WriteBefore(Guid.NewGuid());
        }

        // The command is executed again while the instrumentation is disabled.
        WriteBefore(Guid.NewGuid());

        // The instrumentation is enabled again before the command completes, under a parent which is not sampled.
        using var parent = new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(activities)
            .Build();

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
            new
            {
                OperationId = Guid.NewGuid(),
                Operation = "CleanupAfterExecuteNonQueryAsync",
                Command = command,
                Statistics = (IDictionary?)null,
                Timestamp = 2000000L,
            });

        tracerProvider.ForceFlush();

        Assert.Empty(activities);
        Assert.Equal(parent, Activity.Current);
    }

    [Fact]
    public void ActivityIsStoppedWhenInstrumentationIsDisabledWhileCommandExecutes()
    {
        var started = new List<Activity>();
        var sqlActivitySource = SqlTelemetryHelper.ActivitySource;

        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source == sqlActivitySource,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStarted = started.Add,
        };

        ActivitySource.AddActivityListener(activityListener);

        using var listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);

        var command = new FakeDbCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = "SP_GetOrders",
            Connection = new FakeDbConnection(),
        };

        var operationId = Guid.NewGuid();

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .Build();

        try
        {
            listener.Write(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                new
                {
                    OperationId = operationId,
                    Operation = "InternalExecuteNonQueryAsync",
                    Command = command,
                    Timestamp = (long?)1000000L,
                });
        }
        finally
        {
            // The last provider is disposed while the command is executing.
            tracerProvider.Dispose();
        }

        var activity = Assert.Single(started);
        Assert.False(activity.IsStopped);

        // The command completes on an execution context which has a different current activity.
        using var other = new Activity("other").SetIdFormat(ActivityIdFormat.W3C).Start();

        listener.Write(
            SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
            new
            {
                OperationId = operationId,
                Operation = "CleanupAfterExecuteNonQueryAsync",
                Command = command,
                Statistics = (IDictionary?)null,
                Timestamp = 2000000L,
            });

        Assert.True(activity.IsStopped);
        Assert.Equal(other, Activity.Current);
    }

    [Theory]
    [InlineData(SqlClientLibrary.SystemDataSqlClient)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient)]
    public void MetricUsesDatabaseOfConnectionWhenCommandStarted(SqlClientLibrary library)
    {
        foreach (var samplingMode in Enum.GetValues<SamplingMode>())
        {
            var activities = new List<Activity>();
            var metrics = new List<Metric>();

            var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

            if (samplingMode != SamplingMode.MetricsOnly)
            {
                tracerProviderBuilder
                    .SetSampler(samplingMode == SamplingMode.Sampled ? new AlwaysOnSampler() : new AlwaysOffSampler())
                    .AddSqlClientInstrumentation()
                    .AddInMemoryExporter(activities);
            }

            using var tracerProvider = tracerProviderBuilder.Build();
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSqlClientInstrumentation()
                .AddInMemoryExporter(metrics)
                .Build();

            // A connection which breaks while a command executes is closed before the error is
            // reported, after which its database is that of its connection string, not the one
            // the command was executed against.
            var connection = new ChangingDatabaseDbConnection("orders");

            using (samplingMode == SamplingMode.NotSampled
                ? new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start()
                : null)
            {
                MockCommandExecutor.ExecuteCommand(
                    new FakeDbCommand
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandText = "SP_GetOrders",
                        Connection = connection,
                    },
                    library,
                    statistics: null,
                    afterBeforeCommand: () => connection.CurrentDatabase = "master");
            }

            Assert.True(tracerProvider.ForceFlush());
            Assert.True(meterProvider.ForceFlush());

            Assert.Equal(samplingMode == SamplingMode.Sampled ? 1 : 0, activities.Count);

            if (samplingMode == SamplingMode.Sampled)
            {
                Assert.Equal("orders", Assert.Single(activities).GetTagValue(SemanticConventions.AttributeDbNamespace));
            }

            var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
            var metricPoints = new List<MetricPoint>();
            foreach (var point in metric.GetMetricPoints())
            {
                metricPoints.Add(point);
            }

            var metricPoint = Assert.Single(metricPoints);
            var tags = new Dictionary<string, object?>();
            foreach (var tag in metricPoint.Tags)
            {
                tags.Add(tag.Key, tag.Value);
            }

            Assert.True(
                tags.TryGetValue(SemanticConventions.AttributeDbNamespace, out var dbNamespace) && "orders".Equals(dbNamespace),
                $"Unexpected db.namespace '{dbNamespace}' for {samplingMode}.");
        }
    }
#endif

    [Theory]
    [InlineData(SqlClientLibrary.SystemDataSqlClient)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient)]
    public void UnsampledTextCommandMetricIncludesQuerySummary(SqlClientLibrary library)
    {
        var metrics = new List<Metric>();

        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new TestSampler
            {
                SamplingAction = _ => new SamplingResult(SamplingDecision.Drop),
            })
            .AddSqlClientInstrumentation()
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        MockCommandExecutor.ExecuteCommand(
            TestConnectionString,
            CommandType.Text,
            "select * from sys.databases",
            error: false,
            library);

        traceProvider.ForceFlush();
        meterProvider.ForceFlush();

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        var metricPoints = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            metricPoints.Add(point);
        }

        var metricPoint = Assert.Single(metricPoints);
        object? querySummary = null;
        foreach (var tag in metricPoint.Tags)
        {
            if (tag.Key == SemanticConventions.AttributeDbQuerySummary)
            {
                querySummary = tag.Value;
                break;
            }
        }

        Assert.Equal("select sys.databases", querySummary);
    }

    [Theory]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.Text, false)]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.Text, true)]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.StoredProcedure, false)]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.StoredProcedure, true)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.Text, false)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.Text, true)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.StoredProcedure, false)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.StoredProcedure, true)]
    public void MetricTagsDoNotDependOnSamplingDecision(SqlClientLibrary library, CommandType commandType, bool error)
    {
        var commandText = commandType == CommandType.StoredProcedure ? "SP_GetOrders" : "select * from sys.databases";

        var expected = GetMetricTags(library, commandType, commandText, error, SamplingMode.Sampled);

        Assert.Equal(SqlTelemetryHelper.MicrosoftSqlServerDbSystemName, expected[SemanticConventions.AttributeDbSystemName]);
#if NETFRAMEWORK
        // The .NET Framework events are always written for the master database.
        Assert.Equal("MSSQLLocalDB.master", expected[SemanticConventions.AttributeDbNamespace]);
#else
        Assert.Equal("MSSQLLocalDB.main", expected[SemanticConventions.AttributeDbNamespace]);
#endif
        Assert.Equal("(localdb)", expected[SemanticConventions.AttributeServerAddress]);
        Assert.Equal(error, expected.ContainsKey(SemanticConventions.AttributeErrorType));

        if (commandType == CommandType.Text)
        {
            Assert.Equal("select sys.databases", expected[SemanticConventions.AttributeDbQuerySummary]);
        }

#if !NETFRAMEWORK
        if (commandType == CommandType.StoredProcedure)
        {
            Assert.Equal("EXECUTE", expected[SemanticConventions.AttributeDbOperationName]);
            Assert.Equal(commandText, expected[SemanticConventions.AttributeDbStoredProcedureName]);
            Assert.Equal($"EXECUTE {commandText}", expected[SemanticConventions.AttributeDbQuerySummary]);
        }
#endif

        Assert.Equal(expected, GetMetricTags(library, commandType, commandText, error, SamplingMode.NotRecorded));
        Assert.Equal(expected, GetMetricTags(library, commandType, commandText, error, SamplingMode.NotSampled));
        Assert.Equal(expected, GetMetricTags(library, commandType, commandText, error, SamplingMode.MetricsOnly));
    }

    [Theory]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.Text, false)]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.Text, true)]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.StoredProcedure, false)]
    [InlineData(SqlClientLibrary.SystemDataSqlClient, CommandType.StoredProcedure, true)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.Text, false)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.Text, true)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.StoredProcedure, false)]
    [InlineData(SqlClientLibrary.MicrosoftDataSqlClient, CommandType.StoredProcedure, true)]
    public void MetricTagsDoNotIncludeTagsSetOnActivityByOthers(SqlClientLibrary library, CommandType commandType, bool error)
    {
        var commandText = commandType == CommandType.StoredProcedure ? "SP_GetOrders" : "select * from sys.databases";

        var expected = GetMetricTags(library, commandType, commandText, error, SamplingMode.MetricsOnly);

        // Simulates something other than the instrumentation, such as BaggageActivityProcessor
        // copying the baggage of an incoming request, setting tags on the command's activity,
        // either when it starts (so they replace or precede the tags of the instrumentation) or
        // by the time it ends. None of them can be allowed to become dimensions of the metric.
        static void SetTags(Activity activity)
        {
            foreach (var name in new[]
            {
                SemanticConventions.AttributeDbSystem,
                SemanticConventions.AttributeDbSystemName,
                SemanticConventions.AttributeDbNamespace,
                SemanticConventions.AttributeDbOperationName,
                SemanticConventions.AttributeDbStoredProcedureName,
                SemanticConventions.AttributeDbQuerySummary,
                SemanticConventions.AttributeDbResponseStatusCode,
                SemanticConventions.AttributeErrorType,
                SemanticConventions.AttributeServerAddress,
                SemanticConventions.AttributeServerPort,
            })
            {
                activity.SetTag(name, "caller-supplied-" + name);
            }
        }

        var actual = GetMetricTags(library, commandType, commandText, error, SamplingMode.Sampled, SetTags);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain(actual.Values, value => value is string text && text.StartsWith("caller-supplied-", StringComparison.Ordinal));
        Assert.DoesNotContain(SemanticConventions.AttributeDbSystem, actual.Keys);
    }

    private static SortedDictionary<string, object?> GetMetricTags(
        SqlClientLibrary library,
        CommandType commandType,
        string commandText,
        bool error,
        SamplingMode samplingMode,
        Action<Activity>? setActivityTags = null)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (samplingMode != SamplingMode.MetricsOnly)
        {
            tracerProviderBuilder
                .SetSampler(samplingMode == SamplingMode.Sampled ? new AlwaysOnSampler() : new AlwaysOffSampler())
                .AddSqlClientInstrumentation()
                .AddProcessor(new TestActivityProcessor(onStart: setActivityTags ?? (_ => { }), onEnd: setActivityTags ?? (_ => { })))
                .AddInMemoryExporter(activities);
        }

        using var tracerProvider = tracerProviderBuilder.Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        // A command which is not sampled is created as an activity which is not recorded if it is
        // the root of a trace, and is not created at all if its local parent is not sampled.
        using (var parent = samplingMode == SamplingMode.NotSampled
            ? new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start()
            : null)
        {
            MockCommandExecutor.ExecuteCommand(TestConnectionString, commandType, commandText, error, library);
        }

        Assert.True(tracerProvider.ForceFlush());
        Assert.True(meterProvider.ForceFlush());

        Assert.Equal(samplingMode == SamplingMode.Sampled ? 1 : 0, activities.Count);

        var metric = Assert.Single(metrics, m => m.Name == "db.client.operation.duration");
        var metricPoints = new List<MetricPoint>();
        foreach (var point in metric.GetMetricPoints())
        {
            metricPoints.Add(point);
        }

        var metricPoint = Assert.Single(metricPoints);
        Assert.Equal(1, metricPoint.GetHistogramCount());

        var tags = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in metricPoint.Tags)
        {
            tags.Add(tag.Key, tag.Value);
        }

        return tags;
    }

    private static void VerifyAttributes(SqlClientTestCase testCase, Activity activity, MetricPoint metricPoint)
    {
        var metricAttributes = new Dictionary<string, object?>();
        foreach (var tag in metricPoint.Tags)
        {
            metricAttributes[tag.Key] = tag.Value;
        }

        Assert.Equal(testCase.Expected.DbCollectionName, activity.GetTagValue(SemanticConventions.AttributeDbCollectionName));
        Assert.Equal(testCase.Expected.DbNamespace, activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        Assert.Equal(testCase.Expected.DbOperationBatchSize, activity.GetTagValue(SemanticConventions.AttributeDbOperationBatchSize));
        Assert.Equal(testCase.Expected.DbOperationName, activity.GetTagValue(SemanticConventions.AttributeDbOperationName));
        Assert.Equal(testCase.Expected.DbQuerySummary, activity.GetTagValue(SemanticConventions.AttributeDbQuerySummary));
        Assert.Equal(testCase.Expected.DbQueryText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
        Assert.Equal(testCase.Expected.DbResponseStatusCode, activity.GetTagValue(SemanticConventions.AttributeDbResponseStatusCode));
        Assert.Equal(testCase.Expected.DbStoredProcedureName, activity.GetTagValue(SemanticConventions.AttributeDbStoredProcedureName));
        Assert.Equal(testCase.Expected.DbSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystemName));
        Assert.Equal(testCase.Expected.ErrorType, activity.GetTagValue(SemanticConventions.AttributeErrorType));
        Assert.Equal(testCase.Expected.ServerAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(testCase.Expected.ServerPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));

        Assert.Equal(testCase.Expected.DbCollectionName, metricAttributes.TryGetValue(SemanticConventions.AttributeDbCollectionName, out var value) ? value : null);
        Assert.Equal(testCase.Expected.DbNamespace, metricAttributes.TryGetValue(SemanticConventions.AttributeDbNamespace, out value) ? value : null);
        Assert.Equal(testCase.Expected.DbOperationName, metricAttributes.TryGetValue(SemanticConventions.AttributeDbOperationName, out value) ? value : null);
        Assert.Equal(testCase.Expected.DbQuerySummary, metricAttributes.TryGetValue(SemanticConventions.AttributeDbQuerySummary, out value) ? value : null);
        Assert.Equal(testCase.Expected.DbResponseStatusCode, metricAttributes.TryGetValue(SemanticConventions.AttributeDbResponseStatusCode, out value) ? value : null);
        Assert.Equal(testCase.Expected.DbStoredProcedureName, metricAttributes.TryGetValue(SemanticConventions.AttributeDbStoredProcedureName, out value) ? value : null);
        Assert.Equal(testCase.Expected.DbSystemName, metricAttributes.TryGetValue(SemanticConventions.AttributeDbSystemName, out value) ? value : null);
        Assert.Equal(testCase.Expected.ErrorType, metricAttributes.TryGetValue(SemanticConventions.AttributeErrorType, out value) ? value : null);
        Assert.Equal(testCase.Expected.ServerAddress, metricAttributes.TryGetValue(SemanticConventions.AttributeServerAddress, out value) ? value : null);
        Assert.Equal(testCase.Expected.ServerPort, metricAttributes.TryGetValue(SemanticConventions.AttributeServerPort, out value) ? value : null);
    }

    private static void VerifySamplingParameters(SqlClientTestCase testCase, SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystemName
                   && kvp.Value is string value
                   && value == SqlTelemetryHelper.MicrosoftSqlServerDbSystemName);

        if (testCase.Expected.DbNamespace != null)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbNamespace
                       && kvp.Value is string value
                       && value == testCase.Expected.DbNamespace);
        }

        if (testCase.Expected.ServerAddress != null)
        {
            Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeServerAddress
                   && kvp.Value is string value
                   && value == testCase.Expected.ServerAddress);
        }

        if (testCase.Expected.ServerPort.HasValue)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeServerPort
                       && kvp.Value is int value
                       && value == testCase.Expected.ServerPort);
        }

        if (!string.IsNullOrEmpty(testCase.Expected.DbQuerySummary))
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbQuerySummary
                       && kvp.Value is string value
                       && value == testCase.Expected.DbQuerySummary);
        }

        if (!string.IsNullOrEmpty(testCase.Expected.DbQueryText))
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbQueryText
                       && kvp.Value is string value
                       && value == testCase.Expected.DbQueryText);
        }

        if (!string.IsNullOrEmpty(testCase.Expected.DbOperationName))
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbOperationName
                       && kvp.Value is string value
                       && value == testCase.Expected.DbOperationName);
        }

        if (!string.IsNullOrEmpty(testCase.Expected.DbCollectionName))
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbCollectionName
                       && kvp.Value is string value
                       && value == testCase.Expected.DbCollectionName);
        }

        if (!string.IsNullOrEmpty(testCase.Expected.DbStoredProcedureName))
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbStoredProcedureName
                       && kvp.Value is string value
                       && value == testCase.Expected.DbStoredProcedureName);
        }
    }

    private void RunSqlClientTestCase(SqlClientTestCase testCase, SqlClientLibrary library)
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var sampler = new TestSampler
        {
            SamplingAction = _ => new SamplingResult(SamplingDecision.RecordAndSample),
        };

        using var traceProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddSqlClientInstrumentation(options =>
            {
#if NET
                options.RecordException = true;
#endif
            })
            .AddInMemoryExporter(activities)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        MockCommandExecutor.ExecuteCommand(
            testCase.Input.ConnectionString,
            testCase.Input.CommandType,
            testCase.Input.CommandText,
            testCase.Expected.ErrorType != null,
            library);

        traceProvider.ForceFlush();
        meterProvider.ForceFlush();

        Activity? activity = null;

        activity = Assert.Single(activities);

        Assert.Equal(testCase.Expected.SpanName, activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        if (testCase.Expected.ErrorType == null)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
#if NETFRAMEWORK
            Assert.NotNull(activity.StatusDescription);
#else
            Assert.Null(activity.StatusDescription);
#endif
            var events = activity.Events.ToList();
            var @event = Assert.Single(events);
            Assert.Equal(SemanticConventions.AttributeExceptionEventName, @event.Name);
        }

        var dbClientOperationDurationMetrics = metrics
            .Where(metric => metric.Name == "db.client.operation.duration")
            .ToArray();

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
        var sum = metricPoint.GetHistogramSum();
        Assert.Equal(activity.Duration.TotalSeconds, sum);

        VerifySamplingParameters(testCase, sampler.LatestSamplingParameters);
        VerifyAttributes(testCase, activity, metricPoint);
    }

#if !NETFRAMEWORK
    private sealed class ChangingDatabaseDbConnection(string database) : FakeDbConnection
    {
        public string CurrentDatabase { get; set; } = database;

        public override string Database => this.CurrentDatabase;
    }
#endif
}
