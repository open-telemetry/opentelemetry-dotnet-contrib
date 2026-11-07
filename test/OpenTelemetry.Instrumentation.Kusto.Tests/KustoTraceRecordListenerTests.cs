// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

// The listener and activity source are process-wide, so this must not run alongside the other tests that use them.
[Collection("KustoInstrumentation")]
public class KustoTraceRecordListenerTests : IDisposable
{
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
