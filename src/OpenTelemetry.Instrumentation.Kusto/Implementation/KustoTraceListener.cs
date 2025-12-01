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

    private void HandleException(KustoUtils.TraceRecord record)
    {
        var activity = this.GetActivity(record);
        var result = TraceRecordParser.ParseException(record.Message.AsSpan());
        activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage.ToString());
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

            var result = TraceRecordParser.ParseRequestStart(record.Message.AsSpan());

            if (!string.IsNullOrEmpty(result.Uri))
            {
                activity.SetTag(SemanticConventions.AttributeUrlFull, result.Uri);
            }

            if (!string.IsNullOrEmpty(result.ServerAddress))
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, result.ServerAddress);
            }

            if (result.ServerPort is not null)
            {
                activity.SetTag(SemanticConventions.AttributeServerPort, result.ServerPort.Value);
            }

            if (!result.Database.IsEmpty)
            {
                activity.SetTag(SemanticConventions.AttributeDbNamespace, result.Database.ToString());
            }

            if (!result.QueryText.IsEmpty)
            {
                var info = KustoProcessor.Process(shouldSummarize: KustoInstrumentation.TracingOptions.RecordQuerySummary, shouldSanitize: KustoInstrumentation.TracingOptions.RecordQueryText, result.QueryText.ToString());

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

            try
            {
                KustoInstrumentation.TracingOptions.Enrich?.Invoke(activity, record);
            }
            catch (Exception ex)
            {
                KustoInstrumentationEventSource.Log.EnrichmentException(ex);
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
            var result = TraceRecordParser.ParseActivityComplete(record.Message.AsSpan());
            if (result.HowEnded.Equals("Success".AsSpan(), StringComparison.Ordinal))
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
