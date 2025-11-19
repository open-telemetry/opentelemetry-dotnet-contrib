// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal sealed class KustoTraceListener : KustoUtils.ITraceListener
{
    private readonly ConcurrentDictionary<Guid, Activity> activities = new();

    public KustoTraceListener()
    {
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

        if (!KustoInstrumentation.HandleManager.IsTracingActive())
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

    private static ReadOnlySpan<char> ExtractValueBetween(ReadOnlySpan<char> source, ReadOnlySpan<char> start, ReadOnlySpan<char> end)
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
        var schemeEnd = uri.IndexOf("://".AsSpan());
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

    private void HandleException(KustoUtils.TraceRecord record)
    {
        var activity = this.GetActivity(record);
        var message = ExtractValueBetween(record.Message.AsSpan(), "ErrorMessage=".AsSpan(), Environment.NewLine.AsSpan());
        activity?.SetStatus(ActivityStatusCode.Error, message.ToString());
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

            var uri = ExtractValueBetween(message, "Uri=".AsSpan(), ",".AsSpan());
            if (!uri.IsEmpty)
            {
                var uriString = uri.ToString();
                activity.SetTag(SemanticConventions.AttributeUrlFull, uriString);
                activity.SetTag(SemanticConventions.AttributeServerAddress, GetServerAddress(uri));

                string? database = null; // TODO: Add parsing for database when available
                if (!string.IsNullOrEmpty(database))
                {
                    activity.SetTag(SemanticConventions.AttributeDbNamespace, database);
                }
            }

            // Extract and parse query text
            var text = ExtractValueBetween(message, "text=".AsSpan(), Environment.NewLine.AsSpan());

            if (!text.IsEmpty)
            {
                var queryText = text.ToString();
                var info = KustoProcessor.Process(shouldSummarize: true, shouldSanitize: KustoInstrumentation.TracingOptions.RecordQueryText, queryText);

                // Set sanitized query text if configured
                if (KustoInstrumentation.TracingOptions.RecordQueryText)
                {
                    activity.SetTag(SemanticConventions.AttributeDbQueryText, info.Sanitized);
                }

                // Set query summary and use it as display name per spec
                if (!string.IsNullOrEmpty(info.Summarized))
                {
                    activity.SetTag(SemanticConventions.AttributeDbQuerySummary, info.Summarized);
                    activity.DisplayName = info.Summarized!;
                }
                else
                {
                    // Fall back to operation name if no summary available
                    activity.DisplayName = operationName;
                }
            }
            else
            {
                // Fall back to operation name if no query text
                activity.DisplayName = operationName;
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
            var howEnded = ExtractValueBetween(record.Message.AsSpan(), "HowEnded=".AsSpan(), ",".AsSpan());
            if (howEnded.Equals("Success".AsSpan(), StringComparison.Ordinal))
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }

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
