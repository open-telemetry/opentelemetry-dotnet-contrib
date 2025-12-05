// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal sealed class KustoTraceRecordListener : KustoUtils.ITraceListener
{
    // The client's async machinery may not call us back using the same AsyncLocal context, so we must manually track
    // the Activity's ActivityId (which the client guarantees will be unique) with the context data we need.
    private readonly ConcurrentDictionary<Guid, ContextData> contexts = new();

    public override string Name { get; } = nameof(KustoTraceRecordListener);

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

        if (!KustoInstrumentation.HandleManager.IsTracingActive() && !KustoInstrumentation.HandleManager.IsMetricsActive())
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

    private static double GetElapsedTime(long begin)
    {
#if NET
        var duration = Stopwatch.GetElapsedTime(begin);
#else
        var end = Stopwatch.GetTimestamp();
        var timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        var delta = end - begin;
        var ticks = (long)(timestampToTicks * delta);
        var duration = new TimeSpan(ticks);
#endif

        return duration.TotalSeconds;
    }

    private static bool ShouldComputeTags(Activity? activity) =>
        (activity is not null && activity.IsAllDataRequested) || KustoInstrumentation.HandleManager.IsMetricsActive();

    private void CallEnrichment(KustoUtils.TraceRecord record)
    {
        try
        {
            var activity = this.GetContext(record)?.Activity;
            if (activity is not null && activity.IsAllDataRequested)
            {
                KustoInstrumentation.Options.Enrich?.Invoke(activity, record);
            }
        }
        catch (Exception ex)
        {
            KustoInstrumentationEventSource.Log.EnrichmentException(ex);
        }
    }

    private void HandleException(KustoUtils.TraceRecord record)
    {
        var context = this.GetContext(record);
        var activity = context?.Activity;
        if (context is null)
        {
            return;
        }

        var result = TraceRecordParser.ParseException(record.Message.AsSpan());
        if (!result.ErrorType.IsEmpty)
        {
            activity?.SetTag(SemanticConventions.AttributeErrorType, result.ErrorType.ToString());
            context.Value.Tags.Add(SemanticConventions.AttributeErrorType, result.ErrorType.ToString());
        }

        var description = result.ErrorMessage.IsEmpty ? null : result.ErrorMessage.ToString();
        activity?.SetStatus(ActivityStatusCode.Error, description);

        this.CallEnrichment(record);
    }

    private void HandleHttpRequestStart(KustoUtils.TraceRecord record)
    {
        var beginTimestamp = Stopwatch.GetTimestamp();
        var operationName = record.Activity.ActivityType;

        var activity = KustoActivitySourceHelper.ActivitySource.StartActivity(operationName, ActivityKind.Client);
        var tagList = default(TagList);

        if (ShouldComputeTags(activity))
        {
            activity?.DisplayName = operationName;
            activity?.AddTag(KustoActivitySourceHelper.ClientRequestIdTagKey, record.Activity.ClientRequestId.ToString());

            tagList.Add(SemanticConventions.AttributeDbSystemName, KustoActivitySourceHelper.DbSystem);
            tagList.Add(SemanticConventions.AttributeDbOperationName, operationName);

            var result = TraceRecordParser.ParseRequestStart(record.Message.AsSpan());

            if (!string.IsNullOrEmpty(result.ServerAddress))
            {
                tagList.Add(SemanticConventions.AttributeServerAddress, result.ServerAddress);
            }

            if (result.ServerPort is not null)
            {
                tagList.Add(SemanticConventions.AttributeServerPort, result.ServerPort.Value);
            }

            if (!result.Database.IsEmpty)
            {
                tagList.Add(SemanticConventions.AttributeDbNamespace, result.Database.ToString());
            }

            if (!result.QueryText.IsEmpty)
            {
                var info = KustoProcessor.Process(shouldSummarize: KustoInstrumentation.Options.RecordQuerySummary, shouldSanitize: KustoInstrumentation.Options.RecordQueryText, result.QueryText.ToString());

                if (KustoInstrumentation.Options.RecordQueryText)
                {
                    tagList.Add(SemanticConventions.AttributeDbQueryText, info.Sanitized);
                }

                // Set query summary and use it as display name per spec
                if (!string.IsNullOrEmpty(info.Summarized))
                {
                    tagList.Add(SemanticConventions.AttributeDbQuerySummary, info.Summarized);
                    activity?.DisplayName = info.Summarized!;
                }
            }
        }

        this.contexts[record.Activity.ActivityId] = new ContextData(beginTimestamp, tagList, activity!);

        this.CallEnrichment(record);
    }

    private void HandleActivityComplete(KustoUtils.TraceRecord record)
    {
        var context = this.GetContext(record);
        if (context is null)
        {
            return;
        }

        var activity = context.Value.Activity;

        var result = TraceRecordParser.ParseActivityComplete(record.Message.AsSpan());
        if (result.HowEnded.Equals("Success".AsSpan(), StringComparison.Ordinal))
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        activity.AddTags(context.Value.Tags);
        this.CallEnrichment(record);
        activity.Stop();

        var duration = activity?.Duration.TotalSeconds ?? GetElapsedTime(context.Value.BeginTimestamp);
        KustoActivitySourceHelper.OperationDurationHistogram.Record(duration, context.Value.Tags);

        this.contexts.TryRemove(record.Activity.ActivityId, out _);
    }

    private ContextData? GetContext(KustoUtils.TraceRecord record)
    {
        if (this.contexts.TryGetValue(record.Activity.ActivityId, out var context))
        {
            return context;
        }

        return null;
    }

    private readonly struct ContextData
    {
        public ContextData(long beginTimestamp, TagList tags, Activity activity)
        {
            this.BeginTimestamp = beginTimestamp;
            this.Tags = tags;
            this.Activity = activity;
        }

        public long BeginTimestamp { get; }

        public TagList Tags { get; }

        public Activity Activity { get; }
    }
}
