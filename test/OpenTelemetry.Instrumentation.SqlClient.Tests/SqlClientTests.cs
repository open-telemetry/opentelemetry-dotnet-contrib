// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
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
    [MemberData(nameof(SqlTestData.SqlClientCallsAreCollectedSuccessfullyCases), MemberType = typeof(SqlTestData))]
    public void SqlClientCallsAreCollectedSuccessfully(
        string beforeCommand,
        string afterCommand,
        CommandType commandType,
        string commandText,
        bool captureTextCommandContent,
        bool shouldEnrich = true,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false,
        bool tracingEnabled = true,
        bool metricsEnabled = true)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var activities = new List<Activity>();
        var metrics = new List<Metric>();

        var traceProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (tracingEnabled)
        {
            traceProviderBuilder.AddSqlClientInstrumentation(
            (opt) =>
            {
                opt.SetDbStatementForText = captureTextCommandContent;
                if (shouldEnrich)
                {
                    opt.Enrich = ActivityEnrichment;
                }

                opt.EmitOldAttributes = emitOldAttributes;
                opt.EmitNewAttributes = emitNewAttributes;
            });
            traceProviderBuilder.AddInMemoryExporter(activities);
        }

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();

        if (metricsEnabled)
        {
            meterProviderBuilder.AddSqlClientInstrumentation();
            meterProviderBuilder.AddInMemoryExporter(metrics);
        }

        var traceProvider = traceProviderBuilder.Build();
        var meterProvider = meterProviderBuilder.Build();

        try
        {
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
        finally
        {
            traceProvider.Dispose();
            meterProvider.Dispose();
        }

        Activity? activity = null;

        if (tracingEnabled)
        {
            activity = Assert.Single(activities);
            VerifyActivityData(
                sqlCommand.CommandType,
                sqlCommand.CommandText,
                captureTextCommandContent,
                false,
                false,
                shouldEnrich,
                activity,
                emitOldAttributes,
                emitNewAttributes);
        }

        var dbClientOperationDurationMetrics = metrics
            .Where(metric => metric.Name == "db.client.operation.duration")
            .ToArray();

        if (metricsEnabled)
        {
            var metric = Assert.Single(dbClientOperationDurationMetrics);
            VerifyDurationMetricData(metric, activity);
        }
        else
        {
            Assert.Empty(dbClientOperationDurationMetrics);
        }
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
    [MemberData(nameof(SqlTestData.SqlClientErrorsAreCollectedSuccessfullyCases), MemberType = typeof(SqlTestData))]
    public void SqlClientErrorsAreCollectedSuccessfully(
        string beforeCommand,
        string errorCommand,
        bool shouldEnrich = true,
        bool recordException = false,
        bool tracingEnabled = true,
        bool metricsEnabled = true)
    {
        using var sqlConnection = new SqlConnection(TestConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var activities = new List<Activity>();
        var metrics = new List<Metric>();
        var traceProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (tracingEnabled)
        {
            traceProviderBuilder.AddSqlClientInstrumentation(options =>
            {
                options.RecordException = recordException;
                if (shouldEnrich)
                {
                    options.Enrich = ActivityEnrichment;
                }
            })
            .AddInMemoryExporter(activities);
        }

        var traceProvider = traceProviderBuilder.Build();

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();

        if (metricsEnabled)
        {
            meterProviderBuilder
                .AddSqlClientInstrumentation()
                .AddInMemoryExporter(metrics);
        }

        var meterProvider = meterProviderBuilder.Build();

        try
        {
            var operationId = Guid.NewGuid();
            sqlCommand.CommandText = "SP_GetOrders";
            sqlCommand.CommandType = CommandType.StoredProcedure;

            var beforeExecuteEventData = new
            {
                OperationId = operationId,
                Command = sqlCommand,
                Timestamp = (long?)1000000L,
            };

            this.fakeSqlClientDiagnosticSource.Write(
                beforeCommand,
                beforeExecuteEventData);

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
        finally
        {
            traceProvider.Dispose();
            meterProvider.Dispose();
        }

        Activity? activity = null;

        if (tracingEnabled)
        {
            activity = Assert.Single(activities);
            VerifyActivityData(
                sqlCommand.CommandType,
                sqlCommand.CommandText,
                false,
                true,
                recordException,
                shouldEnrich,
                activity);
        }
        else
        {
            Assert.Empty(activities);
        }

        var dbClientOperationDurationMetrics = metrics
            .Where(metric => metric.Name == "db.client.operation.duration")
            .ToArray();

        if (metricsEnabled)
        {
            var metric = Assert.Single(dbClientOperationDurationMetrics);
            VerifyDurationMetricData(metric, activity);
        }
        else
        {
            Assert.Empty(dbClientOperationDurationMetrics);
        }
    }

    [Theory]
    [ClassData(typeof(SqlClientTestCase))]
    public void SqlDataStartsActivityWithExpectedAttributes(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand);
    }

    [Theory]
    [ClassData(typeof(SqlClientTestCase))]
    public void MicrosoftDataStartsActivityWithExpectedAttributes(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand);
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

    internal static void VerifyActivityData(
        CommandType commandType,
        string? commandText,
        bool captureTextCommandContent,
        bool isFailure,
        bool recordException,
        bool shouldEnrich,
        Activity activity,
        bool emitOldAttributes = true,
        bool emitNewAttributes = false)
    {
        if (emitNewAttributes)
        {
            Assert.Equal("MSSQLLocalDB.master", activity.DisplayName);
        }
        else
        {
            Assert.Equal("master", activity.DisplayName);
        }

        Assert.Equal(ActivityKind.Client, activity.Kind);

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);

            if (recordException)
            {
                var events = activity.Events.ToList();
                Assert.Single(events);

                Assert.Equal(SemanticConventions.AttributeExceptionEventName, events[0].Name);
            }
            else
            {
                Assert.Empty(activity.Events);
            }
        }

        if (shouldEnrich)
        {
            Assert.Contains(activity.Tags, tag => tag.Key == "enriched");
            Assert.Equal("yes", activity.Tags.FirstOrDefault(tag => tag.Key == "enriched").Value);
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enriched");
        }

        Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystem));

        if (emitOldAttributes)
        {
            Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbName));
        }

        if (emitNewAttributes)
        {
            Assert.Equal("MSSQLLocalDB.master", activity.GetTagValue(SemanticConventions.AttributeDbNamespace));
        }

        switch (commandType)
        {
            case CommandType.StoredProcedure:
                if (emitOldAttributes)
                {
                    Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                }

                if (emitNewAttributes)
                {
                    Assert.Equal("EXECUTE", activity.GetTagValue(SemanticConventions.AttributeDbOperationName));
                    Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbCollectionName));
                    Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbQueryText));
                }

                break;

            case CommandType.Text:
                if (captureTextCommandContent)
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

                break;
            case CommandType.TableDirect:
                Assert.Fail("Not supported command type: CommandType.TableDirect");
                break;
            default:
                Assert.Fail($"Not supported command type: {commandType}");
                break;
        }
    }

    internal static void VerifyDurationMetricData(Metric metric, Activity? activity)
    {
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
            _ = metricPoint.GetHistogramCount();
            var sum = metricPoint.GetHistogramSum();
            Assert.Equal(activity.Duration.TotalSeconds, sum);
        }
    }

    internal static void VerifySamplingParameters(SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);
        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                   && kvp.Value != null
                   && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName);
    }

    internal static void VerifySamplingParameters(SqlClientTestCase testCase, Activity activity, SamplingParameters samplingParameters)
    {
        Assert.NotNull(samplingParameters.Tags);

        Assert.Equal(testCase.ExpectedActivityName, activity.DisplayName);
        Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName, activity.GetTagItem(SemanticConventions.AttributeDbSystem));
        Assert.Equal(testCase.ExpectedDbNamespace, activity.GetTagItem(SemanticConventions.AttributeDbName));
        Assert.Equal(testCase.ExpectedServerAddress, activity.GetTagItem(SemanticConventions.AttributeServerAddress));
        Assert.Equal(testCase.ExpectedPort, activity.GetTagItem(SemanticConventions.AttributeServerPort));
        Assert.Equal(testCase.ExpectedInstanceName, activity.GetTagItem(SemanticConventions.AttributeDbMsSqlInstanceName));

        Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                   && kvp.Value is string
                   && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName);

        if (testCase.ExpectedDbNamespace != null)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbName
                       && kvp.Value is string
                       && (string)kvp.Value == testCase.ExpectedDbNamespace);
        }

        if (testCase.ExpectedServerAddress != null)
        {
            Assert.Contains(
            samplingParameters.Tags,
            kvp => kvp.Key == SemanticConventions.AttributeServerAddress
                   && kvp.Value is string
                   && (string)kvp.Value == testCase.ExpectedServerAddress);
        }

        if (testCase.ExpectedPort.HasValue)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeServerPort
                       && kvp.Value is int
                       && (int)kvp.Value == testCase.ExpectedPort);
        }

        if (testCase.ExpectedInstanceName != null)
        {
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbMsSqlInstanceName
                       && kvp.Value is string
                       && (string)kvp.Value == testCase.ExpectedInstanceName);
        }
    }

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

#if !NETFRAMEWORK
    private void RunSqlClientTestCase(SqlClientTestCase testCase, string beforeCommand, string afterCommand)
    {
        using var sqlConnection = new SqlConnection(testCase.ConnectionString);
        using var sqlCommand = sqlConnection.CreateCommand();

        var exportedItems = new List<Activity>();

        var sampler = new TestSampler
        {
            SamplingAction = _ => new SamplingResult(SamplingDecision.RecordAndSample),
        };

        using (Sdk.CreateTracerProviderBuilder()
            .AddSqlClientInstrumentation()
            .SetSampler(sampler)
            .AddInMemoryExporter(exportedItems)
            .Build())
        {
            this.fakeSqlClientDiagnosticSource.Write(beforeCommand, new { Command = sqlCommand });
            this.fakeSqlClientDiagnosticSource.Write(afterCommand, new { Command = sqlCommand });
        }

        Assert.Single(exportedItems);
        VerifySamplingParameters(testCase, exportedItems.First(), sampler.LatestSamplingParameters);
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
