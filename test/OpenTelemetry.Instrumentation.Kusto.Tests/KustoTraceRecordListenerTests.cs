// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

[Collection(KustoActivitySourceCollectionDefinition.Name)]
public class KustoTraceRecordListenerTests : IDisposable
{
    private const string QueryActivityType = "KD.RestClient.ExecuteQuery";
    private const string ControlCommandActivityType = "KD.RestClient.ExecuteControlCommand";
    private const string StreamIngestActivityType = "KD.RestClient.ExecuteIngestStreamCommand";
    private const string Secret = "example-literal-value";
    private const string ErrorType = "Kusto.Data.Exceptions.SemanticException";
    private const string ErrorMessage = $"Relop semantic error: SEM0420: Regex pattern is ill-formed: (token={Secret}";

    public KustoTraceRecordListenerTests()
    {
        ResetOptions();
    }

    public void Dispose()
    {
        ResetOptions();
    }

    [Theory]
    [InlineData(QueryActivityType, true)]
    [InlineData(ControlCommandActivityType, true)]
    [InlineData(StreamIngestActivityType, false)]
    [InlineData("KD.RestClient.ExecutedDmRequest", false)]
    [InlineData("KD.RestClient.ExecuteMcpRequest", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasRequestBody(string? activityType, bool expected)
    {
        Assert.Equal(expected, KustoTraceRecordListener.HasRequestBody(activityType));
    }

    [Theory]
    [InlineData(QueryActivityType, "inner;2, text=print number=42", "print number=?")]
    [InlineData(QueryActivityType, "x, text=Forged //, text=print number=42", null)]
    [InlineData(ControlCommandActivityType, "inner;2, text=print number=42", "print number=?")]
    [InlineData(StreamIngestActivityType, "x, text=print number=42", null)]
    [InlineData(StreamIngestActivityType, "x, IpAddresses=([0]: 127.0.0.1), text=print number=42", null)]
    public void QueryTextInNestedActivityWithInjectedClientRequestId(string activityType, string clientRequestId, string? expectedQueryText)
    {
        // Within an existing Kusto activity the client request id of the request is not known, so it cannot be
        // stepped over to find the query text. A stream ingestion request has no body to write to the message,
        // so a "text=" in it can only have come from the (caller supplied) client request id, even when request
        // body tracing is enabled.
        var message = $"$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=TestDB, App=testhost, User=user, ClientVersion=Kusto.Dotnet.Client:{{14.2.4}}, ClientRequestId={clientRequestId}";

        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(KustoActivitySource.ActivitySource.Name)
            .AddInMemoryExporter(activities)
            .Build();

        var listener = new KustoTraceRecordListener
        {
            RequestBodyTraced = () => true,
        };

        listener.TraceOptions.RecordQueryText = true;
        listener.TraceOptions.RecordQuerySummary = true;

        using var handle = listener.HandleManager.AddTracingHandle();

        var outerId = Guid.NewGuid();
        var kustoActivity = KustoUtils.Activity.CreateImportActivity(outerId, Guid.NewGuid(), outerId, "outer;1", activityType);

        listener.Write(new KustoUtils.TraceRecord { Activity = kustoActivity, Message = message });
        listener.Write(new KustoUtils.TraceRecord { Activity = kustoActivity, Message = $"MonitoredActivityCompletedSuccessfully: TestActivityType={activityType}" });

        var activity = Assert.Single(activities);

        Assert.Equal(expectedQueryText, activity.GetTagItem(SemanticConventions.AttributeDbQueryText));
        Assert.Equal(expectedQueryText is not null, activity.GetTagItem(SemanticConventions.AttributeDbQuerySummary) is not null);
    }

    [Fact]
    public void FailedOperationDoesNotExportErrorMessage()
    {
        var activity = RunFailedOperation(recordException: false);

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Equal(ErrorType, activity.GetTagItem("error.type"));
        Assert.Empty(activity.Events);
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Value?.ToString()?.Contains(Secret, StringComparison.Ordinal) is true);
    }

    [Fact]
    public void FailedOperationRecordsErrorMessageAsExceptionEventWhenOptedIn()
    {
        var activity = RunFailedOperation(recordException: true);

        // The message is never used as the status description, even when it is recorded as an event.
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
        Assert.Equal(ErrorType, activity.GetTagItem("error.type"));

        var exceptionEvent = Assert.Single(activity.Events);
        Assert.Equal("exception", exceptionEvent.Name);
        Assert.Equal(ErrorType, exceptionEvent.Tags.Single(t => t.Key == "exception.type").Value);
        Assert.Equal(ErrorMessage, exceptionEvent.Tags.Single(t => t.Key == "exception.message").Value);
    }

    private static void ResetOptions()
    {
        KustoInstrumentation.Listener.TraceOptions = new KustoTraceInstrumentationOptions();
        KustoInstrumentation.Listener.MeterOptions = new KustoMeterInstrumentationOptions();
    }

    private static Activity RunFailedOperation(bool recordException)
    {
        var clientRequestId = Guid.NewGuid().ToString();
        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddKustoInstrumentation(options => options.RecordException = recordException)
            .AddInMemoryExporter(exportedItems)
            .Build())
        {
            var listener = KustoInstrumentation.Listener;
            var kustoActivity = KustoUtils.Activity.CreateTopLevelActivity(new TestActivityType(), clientRequestId);

            listener.Write(new KustoUtils.TraceRecord
            {
                Activity = kustoActivity,
                SourceId = "RestClient2",
                Message = "$$HTTPREQUEST[RestClient2]: Verb=POST, Uri=http://localhost/v1/rest/query, DatabaseName=Operations, text=AppLogs | take 100",
            });

            listener.Write(new KustoUtils.TraceRecord
            {
                Activity = kustoActivity,
                SourceId = "EXC_CTOR.Kusto.Data.Exceptions.SemanticException",
                Message =
                    $"Exception object created: {ErrorType}\n" +
                    "ErrorCode=SEM0420\n" +
                    $"ErrorMessage={ErrorMessage}\n" +
                    "DataSource=http://127.0.0.1:18080/v1/rest/query",
            });

            listener.Write(new KustoUtils.TraceRecord
            {
                Activity = kustoActivity,
                SourceId = "RestClient2",
                Message = "MonitoredActivityCompleted: KD.RestClient.ExecuteQuery",
            });
        }

        return Assert.Single(
            exportedItems,
            item => item.GetTagItem("azure.kusto.client_request_id") is string id && id.Contains(clientRequestId, StringComparison.Ordinal));
    }

    private sealed class TestActivityType : KustoUtils.ActivityType
    {
        public TestActivityType()
            : base("KD.RestClient.ExecuteQuery", "Test activity type")
        {
        }
    }
}
