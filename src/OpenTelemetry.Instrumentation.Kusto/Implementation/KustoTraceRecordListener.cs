// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Class that is registered with the Kusto client library to receive trace records.
/// </summary>
/// <remarks>
/// The Kusto client library uses its own tracing infrastructure. Many types share names with common diagnostic types
/// (e.g. Activity, ITraceListener, etc.) but in the Kusto.Cloud.Platform.Utils namespace.
/// </remarks>
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
            KustoInstrumentationEventSource.Log.NullPayload();
            return;
        }

        if (!KustoInstrumentation.HandleManager.IsTracingActive() && !KustoInstrumentation.HandleManager.IsMetricsActive())
        {
            return;
        }

        try
        {
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
        catch (Exception ex)
        {
            KustoInstrumentationEventSource.Log.UnknownErrorProcessingTraceRecord(ex);
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
                KustoInstrumentation.TraceOptions.Enrich?.Invoke(activity, record);
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
            context.Value.MeterTags.Add(SemanticConventions.AttributeErrorType, result.ErrorType.ToString());
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
        var meterTags = default(TagList);

        if (ShouldComputeTags(activity))
        {
            activity?.DisplayName = operationName;
            activity?.AddTag(KustoActivitySourceHelper.ClientRequestIdTagKey, record.Activity.ClientRequestId.ToString());

            activity?.AddTag(SemanticConventions.AttributeDbSystemName, KustoActivitySourceHelper.DbSystem);
            activity?.AddTag(SemanticConventions.AttributeDbOperationName, operationName);
            meterTags.Add(SemanticConventions.AttributeDbSystemName, KustoActivitySourceHelper.DbSystem);
            meterTags.Add(SemanticConventions.AttributeDbOperationName, operationName);

            var result = TraceRecordParser.ParseRequestStart(record.Message.AsSpan());

            if (!string.IsNullOrEmpty(result.ServerAddress))
            {
                activity?.AddTag(SemanticConventions.AttributeServerAddress, result.ServerAddress);
                meterTags.Add(SemanticConventions.AttributeServerAddress, result.ServerAddress);
            }

            if (result.ServerPort is not null)
            {
                activity?.AddTag(SemanticConventions.AttributeServerPort, result.ServerPort.Value);
                meterTags.Add(SemanticConventions.AttributeServerPort, result.ServerPort.Value);
            }

            if (!result.Database.IsEmpty)
            {
                activity?.AddTag(SemanticConventions.AttributeDbNamespace, result.Database.ToString());
                meterTags.Add(SemanticConventions.AttributeDbNamespace, result.Database.ToString());
            }

            if (!result.QueryText.IsEmpty)
            {
                var shouldSummarize = KustoInstrumentation.TraceOptions.RecordQuerySummary || KustoInstrumentation.MeterOptions.RecordQuerySummary;
                var shouldSanitize = KustoInstrumentation.TraceOptions.RecordQueryText || KustoInstrumentation.MeterOptions.RecordQueryText;
                var info = KustoProcessor.Process(shouldSummarize, shouldSanitize, result.QueryText.ToString());

                if (!string.IsNullOrEmpty(info.Sanitized))
                {
                    if (KustoInstrumentation.TraceOptions.RecordQueryText)
                    {
                        activity?.AddTag(SemanticConventions.AttributeDbQueryText, info.Sanitized);
                    }

                    if (KustoInstrumentation.MeterOptions.RecordQueryText)
                    {
                        meterTags.Add(SemanticConventions.AttributeDbQueryText, info.Sanitized);
                    }
                }

                if (!string.IsNullOrEmpty(info.Summarized))
                {
                    if (KustoInstrumentation.TraceOptions.RecordQuerySummary)
                    {
                        activity?.AddTag(SemanticConventions.AttributeDbQuerySummary, info.Summarized);
                        activity?.DisplayName = info.Summarized!;
                    }

                    if (KustoInstrumentation.MeterOptions.RecordQuerySummary)
                    {
                        meterTags.Add(SemanticConventions.AttributeDbQuerySummary, info.Summarized);
                    }
                }
            }
        }

        this.contexts[record.Activity.ActivityId] = new ContextData(beginTimestamp, meterTags, activity!);

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
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        this.CallEnrichment(record);
        activity?.Stop();

        var duration = activity?.Duration.TotalSeconds ?? GetElapsedTime(context.Value.BeginTimestamp);
        KustoActivitySourceHelper.OperationDurationHistogram.Record(duration, context.Value.MeterTags);

        this.contexts.TryRemove(record.Activity.ActivityId, out _);
    }

    private ContextData? GetContext(KustoUtils.TraceRecord record)
    {
        if (this.contexts.TryGetValue(record.Activity.ActivityId, out var context))
        {
            return context;
        }

        KustoInstrumentationEventSource.Log.ContextNotFound(record.Activity.ActivityId.ToString());
        return null;
    }

    /// <summary>
    /// Holds context data for an ongoing operation.
    /// </summary>
    private readonly struct ContextData
    {
        public ContextData(long beginTimestamp, TagList meterTags, Activity activity)
        {
            this.BeginTimestamp = beginTimestamp;
            this.MeterTags = meterTags;
            this.Activity = activity;
        }

        /// <summary>
        /// Gets the timestamp when the operation began. Used to compute duration if the <see cref="Activity"/>
        /// is not available (i.e. in a metrics-only scenario).
        /// </summary>
        public long BeginTimestamp { get; }

        /// <summary>
        /// Gets the collection of tags associated with the operation that should be applies to metrics.
        /// </summary>
        public TagList MeterTags { get; }

        /// <summary>
        /// Gets the current activity associated with the instance, if any.
        /// </summary>
        /// <remarks>
        /// Will be <see langword="null"/> in a metrics-only scenario.
        /// </remarks>
        public Activity? Activity { get; }
    }
}
