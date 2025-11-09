// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

// TODO: Separate out metrics and add new extensions builder and update README.
// TODO: Can I pare the dependency down to just KustoUtils?

internal sealed class KustoListener : KustoUtils.ITraceListener
{
    internal const string ActivitySourceName = "Kusto.Client";
    private const string InstrumentationVersion = "1.0.0";
    private const string DbSystem = "kusto";

    private const string ClientRequestIdTagKey = "kusto.client_request_id";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, InstrumentationVersion);
    private static readonly Meter Meter = new(ActivitySourceName, InstrumentationVersion);

    // Metrics following OpenTelemetry database semantic conventions
    private static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram(
        SemanticConventions.AttributeDbClientOperationDuration,
        unit: "s",
        advice: new InstrumentAdvice<double>() { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] },
        description: "Duration of database client operations");

    private static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(
        "db.client.operation.count",
        description: "Number of database client operations");

    private readonly KustoInstrumentationOptions options;

    public KustoListener(KustoInstrumentationOptions options)
    {
        this.options = options;
    }

    public override bool IsThreadSafe => true;

    public override void Flush()
    {
    }

    public override void Write(KustoUtils.TraceRecord record)
    {
        if (record?.Message is null)
        {
            return;
        }

        if (record.IsRequestStart())
        {
            this.HandleHttpRequestStart(record);
        }
        else if (record.IsResponseStart())
        {
            HandleHttpResponseReceived(record);
        }
        else if (record.IsException())
        {
            HandleException(record);
        }
    }

    private static void HandleException(KustoUtils.TraceRecord record)
    {
        var activity = Activity.Current;

        activity?.SetStatus(ActivityStatusCode.Error, record.Message);
    }

    private static void HandleHttpResponseReceived(KustoUtils.TraceRecord record)
    {
        var activity = Activity.Current;

        if (activity is null)
        {
            return;
        }

        var clientRequestId = record.Activity.ClientRequestId;
        var activityClientRequestId = activity.GetTagItem(ClientRequestIdTagKey) as string;

        if (clientRequestId.Equals(activityClientRequestId, StringComparison.Ordinal))
        {
            var message = record.Message.AsSpan();
            var statusCode = ExtractValueBetween(message, "StatusCode=", Environment.NewLine);
            CompleteHttpActivity(activity, statusCode);
        }
    }

    // TODO: Revisit this
    private static void CompleteHttpActivity(Activity activity, ReadOnlySpan<char> statusCode)
    {
        if (!statusCode.IsEmpty)
        {
            var statusCodeStr = statusCode.ToString();
            activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, statusCodeStr);

            // Set error status for non-2xx responses
            if (!statusCodeStr.Equals("OK", StringComparison.OrdinalIgnoreCase) &&
                !statusCodeStr.StartsWith('2'))
            {
                activity.SetStatus(ActivityStatusCode.Error);
            }
        }

        var duration = activity.Duration.TotalSeconds;

        // Record metrics
        var tags = new TagList
        {
            { SemanticConventions.AttributeDbSystemName, DbSystem },
            { SemanticConventions.AttributeDbOperationName, activity.DisplayName },
        };

        OperationDurationHistogram.Record(duration, tags);
        OperationCounter.Add(1, tags);

        activity.Stop();
    }

    private static ReadOnlySpan<char> ExtractValueBetween(ReadOnlySpan<char> source, string start, string end)
    {
        var startIndex = source.IndexOf(start);
        if (startIndex < 0)
        {
            return ReadOnlySpan<char>.Empty;
        }

        startIndex += start.Length;
        var remaining = source.Slice(startIndex);

        var endIndex = remaining.IndexOf(end);
        if (endIndex < 0)
        {
            endIndex = remaining.Length;
        }

        return remaining.Slice(0, endIndex);
    }

    private static string GetServerAddress(ReadOnlySpan<char> uri)
    {
        var schemeEnd = uri.IndexOf("://");
        if (schemeEnd < 0)
        {
            return string.Empty;
        }

        var hostStart = schemeEnd + 3;
        var remaining = uri.Slice(hostStart);

        var pathStart = remaining.IndexOf('/');
        var host = pathStart >= 0 ? remaining.Slice(0, pathStart) : remaining;

        return host.ToString();
    }

    private void HandleHttpRequestStart(KustoUtils.TraceRecord record)
    {
        var operationName = record.Activity.ActivityType;

        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Client);

        if (activity?.IsAllDataRequested is true)
        {
            activity.SetTag(SemanticConventions.AttributeDbSystemName, DbSystem);
            activity.SetTag(ClientRequestIdTagKey, record.Activity.ClientRequestId.ToString());
            activity.SetTag(SemanticConventions.AttributeDbOperationName, operationName);

            var message = record.Message.AsSpan();

            var uri = ExtractValueBetween(message, "Uri=", ",");
            if (!uri.IsEmpty)
            {
                var uriString = uri.ToString();
                activity.SetTag(SemanticConventions.AttributeUrlFull, uriString);
                activity.SetTag(SemanticConventions.AttributeServerAddress, GetServerAddress(uri));

                string? database = null; // TODO: Add parsing for database when availble
                if (!string.IsNullOrEmpty(database))
                {
                    activity.SetTag(SemanticConventions.AttributeDbNamespace, database);
                }
            }

            // TODO: Consider adding summary
            if (this.options.RecordQueryText)
            {
                var text = ExtractValueBetween(message, "text=", Environment.NewLine);
                if (!text.IsEmpty)
                {
                    activity.SetTag(SemanticConventions.AttributeDbQueryText, text.ToString());
                }
            }
        }
    }
}
