// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Tests;

public class KustoTraceRecordListenerTests
{
    private const string QueryActivityType = "KD.RestClient.ExecuteQuery";
    private const string ControlCommandActivityType = "KD.RestClient.ExecuteControlCommand";
    private const string StreamIngestActivityType = "KD.RestClient.ExecuteIngestStreamCommand";

    [Theory]
    [InlineData(QueryActivityType, true)]
    [InlineData(ControlCommandActivityType, true)]
    [InlineData(StreamIngestActivityType, false)]
    [InlineData("KD.RestClient.ExcuteDmRequest", false)]
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
}
