// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Benchmarks;

/// <summary>
/// Benchmarks that simulate end-to-end trace and metrics instrumentation by manually creating TraceRecords
/// and passing them through both the trace listener and metrics listener.
/// </summary>
[MemoryDiagnoser]
public class InstrumentationBenchmarks
{
    private static readonly KustoUtils.ActivityType TestActivityType = new FakeActivtyType();

    private readonly Guid activityId = Guid.NewGuid();
    private readonly string clientRequestId = "SW52YWxpZFRhYmxlIHwgdGFrZSAxMA==";

    private KustoTraceListener? traceListener;
    private KustoMetricListener? metricListener;
    private KustoUtils.TraceRecord requestStartRecord = null!;
    private KustoUtils.TraceRecord activityCompleteRecord = null!;
    private KustoUtils.TraceRecord exceptionRecord = null!;
    private TracerProvider? tracerProvider;
    private MeterProvider? meterProvider;
    private IDisposable? tracingHandle;
    private IDisposable? metricHandle;

    [GlobalSetup]
    public void Setup()
    {
        // Setup TracerProvider with the Kusto activity source
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(KustoActivitySourceHelper.ActivitySourceName)
            .Build();

        // Setup MeterProvider with the Kusto meter
        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(KustoActivitySourceHelper.MeterName)
            .Build();

        // Activate instrumentation handles
        this.tracingHandle = KustoInstrumentation.HandleManager.AddTracingHandle();
        this.metricHandle = KustoInstrumentation.HandleManager.AddMetricHandle();

        // Create listeners
        this.traceListener = new KustoTraceListener();
        this.metricListener = new KustoMetricListener();

        // Create TraceRecord instances that simulate a query execution flow
        this.requestStartRecord = CreateRequestStartRecord(
            this.activityId,
            this.clientRequestId,
            "StormEvents | take 10 | where Col1 = 7 | summarize by Date, Time");

        this.activityCompleteRecord = CreateActivityCompleteRecord(
            this.activityId,
            this.clientRequestId);

        this.exceptionRecord = CreateExceptionRecord(
            this.activityId,
            this.clientRequestId);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.tracingHandle?.Dispose();
        this.metricHandle?.Dispose();
        this.tracerProvider?.Dispose();
        this.meterProvider?.Dispose();
    }

    [Benchmark]
    public void SuccessfulQuery()
    {
        // Simulate a successful query execution
        this.traceListener!.Write(this.requestStartRecord);
        this.metricListener!.Write(this.requestStartRecord);

        this.traceListener.Write(this.activityCompleteRecord);
        this.metricListener.Write(this.activityCompleteRecord);
    }

    [Benchmark]
    public void FailedQuery()
    {
        // Simulate a failed query execution
        this.traceListener!.Write(this.requestStartRecord);
        this.metricListener!.Write(this.requestStartRecord);

        this.traceListener.Write(this.exceptionRecord);

        this.traceListener.Write(this.activityCompleteRecord);
        this.metricListener.Write(this.activityCompleteRecord);
    }

    [Benchmark]
    public void TraceListenerOnly()
    {
        // Benchmark just the trace listener
        this.traceListener!.Write(this.requestStartRecord);
        this.traceListener.Write(this.activityCompleteRecord);
    }

    [Benchmark]
    public void MetricListenerOnly()
    {
        // Benchmark just the metric listener
        this.metricListener!.Write(this.requestStartRecord);
        this.metricListener.Write(this.activityCompleteRecord);
    }

    private static KustoUtils.TraceRecord CreateRequestStartRecord(Guid activityId, string clientRequestId, string queryText)
    {
        var message = $$"""$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://127.0.0.1:49902/v1/rest/query, DatabaseName=NetDefaultDB, App=testhost, User=REDMOND\\benchmarkuser, ClientVersion=Kusto.Dotnet.Client:{14.0.2+b2d66614da1a4ff4561c5037c48e5be7002d66d4}|Runtime:{.NET_10.0.0/CLRv10.0.0/10.0.0-rtm.25523.111}, ClientRequestId={{clientRequestId}}, text={{queryText}}""";
        using var context = KustoUtils.Context.PushNewActivityContext(TestActivityType, clientRequestId);

        return KustoUtils.TraceRecord.Create("Kusto.Data", KustoUtils.TraceVerbosity.Verbose, message);
    }

    private static KustoUtils.TraceRecord CreateActivityCompleteRecord(Guid activityId, string clientRequestId)
    {
        const string message = "MonitoredActivityCompletedSuccessfully: TestActivityType=KD.RestClient.ExecuteQuery, Timestamp=2025-12-01T02:30:30.0211167Z, ParentActivityId={0}, Duration=4316.802 [ms], HowEnded=Success";
        using var context = KustoUtils.Context.PushNewActivityContext(TestActivityType, clientRequestId);

        return KustoUtils.TraceRecord.Create("Kusto.Data", KustoUtils.TraceVerbosity.Verbose, message);
    }

    private static KustoUtils.TraceRecord CreateExceptionRecord(Guid activityId, string clientRequestId)
    {
        var message =
            $"""
            Exception object created: Kusto.Data.Exceptions.SemanticException
            [0]Kusto.Data.Exceptions.SemanticException: Semantic error: 'take' operator: Failed to resolve table or column expression named 'InvalidTable'
            Timestamp=2025-12-01T02:39:36.3878585Z
            ClientRequestId={clientRequestId}
            ActivityId={activityId}
            ActivityType=KD.RestClient.ExecuteQuery
            ErrorCode=SEM0100
            ErrorReason=BadRequest
            ErrorMessage='take' operator: Failed to resolve table or column expression named 'InvalidTable'
            DataSource=http://127.0.0.1:62413/v1/rest/query
            DatabaseName=NetDefaultDB
            """;
        using var context = KustoUtils.Context.PushNewActivityContext(TestActivityType, clientRequestId);

        return KustoUtils.TraceRecord.Create("KD.Exceptions", KustoUtils.TraceVerbosity.Error, message);
    }

    private class FakeActivtyType : KustoUtils.ActivityType
    {
        public FakeActivtyType()
            : base("FakeActivity", "A fake activity", KustoUtils.TraceVerbosity.Info, KustoUtils.TraceVerbosity.Info, KustoUtils.TraceVerbosity.Info)
        {
        }
    }
}
