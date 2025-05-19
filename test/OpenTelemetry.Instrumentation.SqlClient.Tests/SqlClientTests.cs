// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Data;
#endif
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;

#if !NETFRAMEWORK
using OpenTelemetry.Tests;
#endif
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

[Collection("SqlClient")]
public class SqlClientTests : IDisposable
{
#if !NETFRAMEWORK
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master";
#endif

    private readonly FakeSqlClientDiagnosticSource fakeSqlClientDiagnosticSource;

    public SqlClientTests()
    {
        this.fakeSqlClientDiagnosticSource = new FakeSqlClientDiagnosticSource();
    }

    public static IEnumerable<object[]> TestData => SqlClientTestCases.GetTestCases();

    public void Dispose()
    {
        this.fakeSqlClientDiagnosticSource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SqlClient_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddSqlClientInstrumentation());
    }

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

    // DiagnosticListener-based instrumentation is only available on .NET Core
#if !NETFRAMEWORK
    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSqlMicrosoftBeforeExecuteCommand(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSqlDataBeforeExecuteCommand(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSqlMicrosoftBeforeExecuteCommandOldConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, emitOldAttributes: true, emitNewAttributes: false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSqlMicrosoftBeforeExecuteCommandOldAndNewConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, emitOldAttributes: true, emitNewAttributes: true);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSqlDataBeforeExecuteCommandOldConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, emitOldAttributes: true, emitNewAttributes: false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSqlDataBeforeExecuteCommandOldAndNewConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, emitOldAttributes: true, emitNewAttributes: true);
    }

    [Theory]
    [InlineData("localhost", "localhost", null, null, null)]
    [InlineData("127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData("127.0.0.1,1434", null, "127.0.0.1", null, 1434)]
    [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", 1818)]

    // Test cases when EmitOldAttributes = false and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database)
    [InlineData("localhost", "localhost", null, null, null, false, true)]
    [InlineData("127.0.0.1,1433", null, "127.0.0.1", null, null, false, true)]
    [InlineData("127.0.0.1,1434", null, "127.0.0.1", null, 1434, false, true)]
    [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", null, 1818, false, true)]

    // Test cases when EmitOldAttributes = true and EmitNewAttributes = true (i.e., OTEL_SEMCONV_STABILITY_OPT_IN=database/dup)
    [InlineData("localhost", "localhost", null, null, null, true, true)]
    [InlineData("127.0.0.1,1433", null, "127.0.0.1", null, null, true, true)]
    [InlineData("127.0.0.1,1434", null, "127.0.0.1", null, 1434, true, true)]
    [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", 1818, true, true)]
    public void SqlClientAddsConnectionLevelAttributes(
        string dataSource,
        string? expectedServerHostName,
        string? expectedServerIpAddress,
        string? expectedInstanceName,
        int? expectedPort,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        var options = new SqlClientTraceInstrumentationOptions()
        {
            EmitOldAttributes = emitOldAttributes,
            EmitNewAttributes = emitNewAttributes,
        };

        var tags = SqlActivitySourceHelper.GetTagListFromConnectionInfo(dataSource, databaseName: null, options, out var _);

        Assert.Equal(expectedServerHostName ?? expectedServerIpAddress, tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeServerAddress).Value);

        if (emitOldAttributes)
        {
            Assert.Equal(expectedInstanceName, tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeDbMsSqlInstanceName).Value);
        }
        else
        {
            Assert.Null(tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeDbMsSqlInstanceName).Value);
        }

        Assert.Equal(expectedPort, tags.FirstOrDefault(x => x.Key == SemanticConventions.AttributeServerPort).Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DbQueryTextCollectedWhenEnabled(bool captureTextCommandContent)
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureTextCommandContent;
                options.EmitOldAttributes = true;
                options.EmitNewAttributes = true;
            })
            .AddInMemoryExporter(activities)
            .Build();

        var commandText = "select * from sys.databases";
        this.ExecuteCommand(TestConnectionString, CommandType.Text, commandText, false, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand);
        this.ExecuteCommand(TestConnectionString, CommandType.Text, commandText, false, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand);

        tracerProvider.ForceFlush();
        Assert.Equal(2, activities.Count);

        if (captureTextCommandContent)
        {
            Assert.Equal(commandText, activities[0].GetTagValue(SemanticConventions.AttributeDbStatement));
            Assert.Equal(commandText, activities[0].GetTagValue(SemanticConventions.AttributeDbQueryText));
            Assert.Equal(commandText, activities[1].GetTagValue(SemanticConventions.AttributeDbStatement));
            Assert.Equal(commandText, activities[1].GetTagValue(SemanticConventions.AttributeDbQueryText));
        }
        else
        {
            Assert.Null(activities[0].GetTagValue(SemanticConventions.AttributeDbStatement));
            Assert.Null(activities[0].GetTagValue(SemanticConventions.AttributeDbQueryText));
            Assert.Null(activities[1].GetTagValue(SemanticConventions.AttributeDbStatement));
            Assert.Null(activities[1].GetTagValue(SemanticConventions.AttributeDbQueryText));
        }
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

        this.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", true, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand);
        this.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", true, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand);

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
                    options.SetDbStatementForText = true;
                    options.RecordException = true;
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

        this.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", false, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand);
        this.ExecuteCommand(TestConnectionString, CommandType.StoredProcedure, "SP_GetOrders", false, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand);

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
                    opt.Enrich = ActivityEnrichment;
                }
            })
            .AddInMemoryExporter(activities)
            .Build();

        this.ExecuteCommand(TestConnectionString, CommandType.Text, "SELECT * FROM Foo", error, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand);
        this.ExecuteCommand(TestConnectionString, CommandType.Text, "SELECT * FROM Foo", error, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand);

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
            cmd =>
            {
                cmd.CommandText = "select 2";
            },
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
            cmd =>
            {
                cmd.CommandText = "select 1";
            },
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
            cmd =>
            {
                cmd.CommandText = "select 1";
            },
            cmd => throw new InvalidOperationException("foobar"));

        Assert.Empty(activities);
    }
#endif

    internal static void ActivityEnrichment(Activity activity, string method, object obj)
    {
        activity.SetTag("enriched", "yes");

        switch (method)
        {
            case "OnCustom":
                Assert.True(obj is SqlCommand);
                break;

            default:
                break;
        }
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

    private static void VerifyOldAttributes(SqlClientTestCase testCase, Activity activity, MetricPoint metricPoint)
    {
        var metricAttributes = new Dictionary<string, object?>();
        foreach (var tag in metricPoint.Tags)
        {
            metricAttributes[tag.Key] = tag.Value;
        }

        Assert.Equal(testCase.ExpectedOldConventions.DbMsSqlInstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        Assert.Equal(testCase.ExpectedOldConventions.DbName, activity.GetTagValue(SemanticConventions.AttributeDbName));
        Assert.Equal(testCase.ExpectedOldConventions.DbSystem, activity.GetTagValue(SemanticConventions.AttributeDbSystem));
        Assert.Equal(testCase.ExpectedOldConventions.DbStatement, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        Assert.Equal(testCase.Expected.ServerAddress, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        Assert.Equal(testCase.Expected.ServerPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));

        Assert.Equal(testCase.ExpectedOldConventions.DbSystem, metricAttributes.TryGetValue(SemanticConventions.AttributeDbSystem, out var value) ? value : null);
        Assert.Equal(testCase.Expected.ServerAddress, metricAttributes.TryGetValue(SemanticConventions.AttributeServerAddress, out value) ? value : null);
        Assert.Equal(testCase.Expected.ServerPort, metricAttributes.TryGetValue(SemanticConventions.AttributeServerPort, out value) ? value : null);
    }

    private static void VerifySamplingParameters(SqlClientTestCase testCase, SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystemName
                   && kvp.Value is string
                   && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDbSystemName);

        if (testCase.Expected.DbNamespace != null)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbNamespace
                       && kvp.Value is string
                       && (string)kvp.Value == testCase.Expected.DbNamespace);
        }

        if (testCase.Expected.ServerAddress != null)
        {
            Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeServerAddress
                   && kvp.Value is string
                   && (string)kvp.Value == testCase.Expected.ServerAddress);
        }

        if (testCase.Expected.ServerPort.HasValue)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeServerPort
                       && kvp.Value is int
                       && (int)kvp.Value == testCase.Expected.ServerPort);
        }
    }

    private static void VerifySamplingParametersOldConventions(SqlClientTestCase testCase, SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                   && kvp.Value is string
                   && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDbSystem);

        if (testCase.ExpectedOldConventions.DbName != null)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbName
                       && kvp.Value is string
                       && (string)kvp.Value == testCase.ExpectedOldConventions.DbName);
        }

        if (testCase.Expected.ServerAddress != null)
        {
            Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeServerAddress
                   && kvp.Value is string
                   && (string)kvp.Value == testCase.Expected.ServerAddress);
        }

        if (testCase.Expected.ServerPort.HasValue)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeServerPort
                       && kvp.Value is int
                       && (int)kvp.Value == testCase.Expected.ServerPort);
        }

        if (testCase.ExpectedOldConventions.DbMsSqlInstanceName != null)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbMsSqlInstanceName
                       && kvp.Value is string
                       && (string)kvp.Value == testCase.ExpectedOldConventions.DbMsSqlInstanceName);
        }
    }

#if !NETFRAMEWORK
    private void RunSqlClientTestCase(SqlClientTestCase testCase, string beforeCommand, bool emitOldAttributes = false, bool emitNewAttributes = true)
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
                options.SetDbStatementForText = true;
                options.RecordException = true;
                options.EmitOldAttributes = emitOldAttributes;
                options.EmitNewAttributes = emitNewAttributes;
            })
            .AddInMemoryExporter(activities)
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSqlClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        this.ExecuteCommand(testCase.Input.ConnectionString, testCase.Input.CommandType, testCase.Input.CommandText, testCase.Expected.ErrorType != null, beforeCommand);

        traceProvider.ForceFlush();
        meterProvider.ForceFlush();

        Activity? activity = null;

        activity = Assert.Single(activities);

        Assert.Equal(emitNewAttributes ? testCase.Expected.SpanName : testCase.ExpectedOldConventions.SpanName, activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        if (testCase.Expected.ErrorType == null)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);
            var events = activity.Events.ToList();
            Assert.Single(events);
            Assert.Equal(SemanticConventions.AttributeExceptionEventName, events[0].Name);
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

        if (emitNewAttributes)
        {
            VerifySamplingParameters(testCase, sampler.LatestSamplingParameters);
            VerifyAttributes(testCase, activity, metricPoint);
        }

        if (emitOldAttributes)
        {
            VerifySamplingParametersOldConventions(testCase, sampler.LatestSamplingParameters);
            VerifyOldAttributes(testCase, activity, metricPoint);
        }
    }

    private Activity[] RunCommandWithFilter(
        Action<SqlCommand> sqlCommandSetup,
        Func<object, bool> filter)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

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
            var operationId = Guid.NewGuid();
            sqlCommandSetup(sqlCommand);

            var beforeExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = (long?)1000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand,
                beforeExecuteEventData);

            var afterExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = 2000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand,
                afterExecuteEventData);
        }

        return [.. activities];
    }

    private void ExecuteCommand(string connectionString, CommandType commandType, string commandText, bool error, string beforeCommand)
    {
        var afterCommand = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
            ? SqlClientDiagnosticListener.SqlDataAfterExecuteCommand
            : SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand;

        var errorCommand = beforeCommand == SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand
            ? SqlClientDiagnosticListener.SqlDataWriteCommandError
            : SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError;

        using var sqlConnection = new SqlConnection(connectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var operationId = Guid.NewGuid();
        sqlCommand.CommandType = commandType;
#pragma warning disable CA2100
        sqlCommand.CommandText = commandText;
#pragma warning restore CA2100

        var beforeExecuteEventData = new
        {
            OperationId = operationId,
            Command = sqlCommand,
            Timestamp = (long?)1000000L,
        };

        this.fakeSqlClientDiagnosticSource.Write(
            beforeCommand,
            beforeExecuteEventData);

        if (error)
        {
            var commandErrorEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Exception = new Exception("Boom!"),
                Timestamp = 2000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                errorCommand,
                commandErrorEventData);
        }
        else
        {
            var afterExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = 2000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                afterCommand,
                afterExecuteEventData);
        }
    }
#endif

    private class FakeSqlClientDiagnosticSource : IDisposable
    {
        private readonly DiagnosticListener listener;

        public FakeSqlClientDiagnosticSource()
        {
            this.listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);
        }

        public void Write(string name, object value)
        {
            if (this.listener.IsEnabled(name))
            {
                this.listener.Write(name, value);
            }
        }

        public void Dispose()
        {
            this.listener.Dispose();
        }
    }
}
