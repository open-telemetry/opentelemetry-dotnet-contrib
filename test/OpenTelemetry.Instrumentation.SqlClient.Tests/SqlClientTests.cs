// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Data;
using System.Diagnostics;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public enum SqlClientLibrary
{
    SystemDataSqlClient,
    MicrosoftDataSqlClient,
}

[Collection("SqlClient")]
public class SqlClientTests : IDisposable
{
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master";

    public static IEnumerable<object[]> TestData => SqlClientTestCases.GetTestCases();

    public void Dispose()
    {
        // TODO: Why is this here? Add comment explaining why.
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SqlClient_BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddSqlClientInstrumentation());
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestMicrosoftDataSqlClient(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientLibrary.MicrosoftDataSqlClient);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSystemDataSqlClient(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientLibrary.SystemDataSqlClient);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestMicrosoftDataSqlClientOldConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientLibrary.MicrosoftDataSqlClient, emitOldAttributes: true, emitNewAttributes: false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestMicrosoftDataSqlClientOldAndNewConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientLibrary.MicrosoftDataSqlClient, emitOldAttributes: true, emitNewAttributes: true);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSystemDataSqlClientOldConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientLibrary.SystemDataSqlClient, emitOldAttributes: true, emitNewAttributes: false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void TestSystemDataSqlClientOldAndNewConventions(SqlClientTestCase testCase)
    {
        this.RunSqlClientTestCase(testCase, SqlClientLibrary.SystemDataSqlClient, emitOldAttributes: true, emitNewAttributes: true);
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
                .AddSqlClientInstrumentation(options => options.RecordException = true)
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

    private void RunSqlClientTestCase(
        SqlClientTestCase testCase,
        SqlClientLibrary library,
        bool emitOldAttributes = false,
        bool emitNewAttributes = true)
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
}
