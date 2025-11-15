// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal sealed class KustoTraceListener : KustoUtils.ITraceListener
{
    private readonly KustoInstrumentationOptions options;
    private readonly ConcurrentDictionary<Guid, Activity> activities = new();

    public KustoTraceListener(KustoInstrumentationOptions options)
    {
        this.options = options;
    }

    public override string Name => nameof(KustoTraceListener);

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
        else if (record.IsActivityComplete())
        {
            this.HandleActivityComplete(record);
        }
        else if (record.IsException())
        {
            this.HandleException(record);
        }
    }

    private void HandleException(KustoUtils.TraceRecord record)
    {
        var activity = this.GetActivity(record);
        var message = ExtractValueBetween(record.Message.AsSpan(), "ErrorMessage=", Environment.NewLine);
        activity?.SetStatus(ActivityStatusCode.Error, message.ToString());
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

        var activity = KustoActivitySourceHelper.ActivitySource.StartActivity(operationName, ActivityKind.Client);
        if (activity is not null)
        {
            this.activities[record.Activity.ActivityId] = activity;
        }

        if (activity?.IsAllDataRequested is true)
        {
            activity.SetTag(SemanticConventions.AttributeDbSystemName, KustoActivitySourceHelper.DbSystem);
            activity.SetTag(KustoActivitySourceHelper.ClientRequestIdTagKey, record.Activity.ClientRequestId.ToString());
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

    private void HandleActivityComplete(KustoUtils.TraceRecord record)
    {
        var activity = this.GetActivity(record);
        if (activity is null)
        {
            return;
        }

        var clientRequestId = record.Activity.ClientRequestId;
        var activityClientRequestId = activity.GetTagItem(KustoActivitySourceHelper.ClientRequestIdTagKey) as string;

        if (clientRequestId.Equals(activityClientRequestId, StringComparison.Ordinal))
        {
            activity.Stop();
        }

#if NET
        this.activities.Remove(record.Activity.ActivityId, out _);
#else
        ((IDictionary<Guid, Activity>)this.activities).Remove(record.Activity.ActivityId);
#endif
    }

    private Activity? GetActivity(KustoUtils.TraceRecord record)
    {
        if (Activity.Current is not null)
        {
            return Activity.Current;
        }

        if (this.activities.TryGetValue(record.Activity.ActivityId, out var activity))
        {
            return activity;
        }

        return null;
    }
}
