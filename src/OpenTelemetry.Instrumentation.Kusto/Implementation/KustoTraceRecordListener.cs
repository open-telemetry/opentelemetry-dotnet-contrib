// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;
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
    // record.Activity (the Kusto-side activity object) is a stable reference across the start, exception, and
    // complete callbacks for a single operation, and the client releases it once the operation finishes. Keying a
    // ConditionalWeakTable on it lets an operation that never reports completion be evicted automatically when the
    // client drops its reference. No manual bookkeeping nor unbounded growth.
#if NET
    private readonly ConditionalWeakTable<object, OperationContext> contexts = [];
#else
    private readonly ConditionalWeakTable<object, OperationContext> contexts = new();
#endif

    /// <summary>
    /// Gets or sets the trace options applied when emitting spans.
    /// </summary>
    public KustoTraceInstrumentationOptions TraceOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the meter options applied when emitting metrics.
    /// </summary>
    public KustoMeterInstrumentationOptions MeterOptions { get; set; } = new();

    /// <summary>
    /// Gets the <see cref="InstrumentationHandleManager"/> that tracks whether any tracing or metrics providers
    /// are currently active.
    /// </summary>
    public InstrumentationHandleManager HandleManager { get; } = new();

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

        if (!this.HandleManager.IsTracingActive() && !this.HandleManager.IsMetricsActive())
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

    private static double GetElapsedTime(long begin) => Stopwatch.GetElapsedTime(begin).TotalSeconds;

    private bool ShouldComputeTags(Activity? activity) =>
        (activity?.IsAllDataRequested is true) || this.HandleManager.IsMetricsActive();

    private void CallEnrichment(KustoUtils.TraceRecord record)
    {
        try
        {
            var activity = this.GetContext(record)?.Activity;
            if (activity?.IsAllDataRequested is true)
            {
                this.TraceOptions.Enrich?.Invoke(activity, record);
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
        if (context is null)
        {
            return;
        }

        var activity = context.Activity;

        var result = TraceRecordParser.ParseException(record.Message.AsSpan());
        if (!result.ErrorType.IsEmpty)
        {
            context.RecordErrorType(result.ErrorType.ToString());
        }

        if (activity is not null)
        {
            var description = result.ErrorMessage.IsEmpty ? null : result.ErrorMessage.ToString();
            activity.SetStatus(ActivityStatusCode.Error, description);
        }

        this.CallEnrichment(record);
    }

    private void HandleHttpRequestStart(KustoUtils.TraceRecord record)
    {
        var beginTimestamp = Stopwatch.GetTimestamp();
        var operationName = record.Activity.ActivityType;

        var activity = KustoActivitySource.ActivitySource.StartActivity(operationName, ActivityKind.Client);

        var context = new OperationContext(beginTimestamp, activity);

        // Store the context before computing tags so that if anything below throws, the completion handler can
        // still find it, stop the Activity, and record the duration instead of leaking a never-stopped Activity.
        this.StoreContext(record.Activity, context);

        if (this.ShouldComputeTags(activity))
        {
            activity?.DisplayName = operationName;
            activity?.AddTag(KustoSemanticConventions.ClientRequestIdTagKey, record.Activity.ClientRequestId.ToString());

            activity?.AddTag(SemanticConventions.AttributeDbSystemName, KustoSemanticConventions.DbSystemNameValue);
            activity?.AddTag(SemanticConventions.AttributeDbOperationName, operationName);
            context.AddMeterTag(SemanticConventions.AttributeDbSystemName, KustoSemanticConventions.DbSystemNameValue);
            context.AddMeterTag(SemanticConventions.AttributeDbOperationName, operationName);

            var result = TraceRecordParser.ParseRequestStart(record.Message.AsSpan());

            if (!string.IsNullOrEmpty(result.ServerAddress))
            {
                activity?.AddTag(SemanticConventions.AttributeServerAddress, result.ServerAddress);
                context.AddMeterTag(SemanticConventions.AttributeServerAddress, result.ServerAddress);
            }

            if (result.ServerPort is not null)
            {
                activity?.AddTag(SemanticConventions.AttributeServerPort, result.ServerPort.Value);
                context.AddMeterTag(SemanticConventions.AttributeServerPort, result.ServerPort.Value);
            }

            if (!result.Database.IsEmpty)
            {
                activity?.AddTag(SemanticConventions.AttributeDbNamespace, result.Database.ToString());
                context.AddMeterTag(SemanticConventions.AttributeDbNamespace, result.Database.ToString());
            }

            if (!result.QueryText.IsEmpty)
            {
                var shouldSummarize = this.TraceOptions.RecordQuerySummary || this.MeterOptions.RecordQuerySummary;
                var shouldSanitize = this.TraceOptions.RecordQueryText || this.MeterOptions.RecordQueryText;
                var info = KustoProcessor.Process(shouldSummarize, shouldSanitize, result.QueryText.ToString());

                if (!string.IsNullOrEmpty(info.Sanitized))
                {
                    if (this.TraceOptions.RecordQueryText)
                    {
                        activity?.AddTag(SemanticConventions.AttributeDbQueryText, info.Sanitized);
                    }

                    if (this.MeterOptions.RecordQueryText)
                    {
                        context.AddMeterTag(SemanticConventions.AttributeDbQueryText, info.Sanitized);
                    }
                }

                if (info.Summarized is { Length: > 0 } summarized)
                {
                    if (this.TraceOptions.RecordQuerySummary)
                    {
                        activity?.AddTag(SemanticConventions.AttributeDbQuerySummary, summarized);
                        activity?.DisplayName = summarized;
                    }

                    if (this.MeterOptions.RecordQuerySummary)
                    {
                        context.AddMeterTag(SemanticConventions.AttributeDbQuerySummary, summarized);
                    }
                }
            }
        }

        this.CallEnrichment(record);
    }

    private void HandleActivityComplete(KustoUtils.TraceRecord record)
    {
        var context = this.GetContext(record);
        if (context is null)
        {
            return;
        }

        var activity = context.Activity;

        // For a single operation the client reports any exception before it reports completion, so we do not
        // guard the window between stopping the Activity and removing the context. A late exception arriving
        // concurrently after the Stop below could tag an already-stopped span and produce telemetry that is
        // inconsistent between the span and the metric; this is an accepted edge case the client does not exhibit.
        if (activity is not null)
        {
            this.CallEnrichment(record);
            activity.Stop();
        }

        var duration = activity?.Duration.TotalSeconds ?? GetElapsedTime(context.BeginTimestamp);
        KustoMetrics.OperationDurationHistogram.Record(duration, context.GetMeterTags());

        this.contexts.Remove(record.Activity);
    }

    private void StoreContext(object key, OperationContext context)
    {
        // Overwrite any existing entry for the key. The client starts a given activity once and reports its
        // start, exception, and complete callbacks sequentially, so the same key is never accessed
        // concurrently and this overwrite does not need to be atomic.
#if NET
        this.contexts.AddOrUpdate(key, context);
#else
        // AddOrUpdate isn't available on these targets, so Remove + Add is the (non-atomic, but per the note
        // above that's fine) equivalent. A plain Add would throw if the key were already present.
        this.contexts.Remove(key);
        this.contexts.Add(key, context);
#endif
    }

    private OperationContext? GetContext(KustoUtils.TraceRecord record)
    {
        if (this.contexts.TryGetValue(record.Activity, out var context))
        {
            return context;
        }

        KustoInstrumentationEventSource.Log.ContextNotFound(record.Activity.ActivityId.ToString());
        return null;
    }

    /// <summary>
    /// Holds context data for an ongoing operation. The Kusto client can invoke the listener concurrently, so the
    /// mutable meter tags are guarded by a lock and updated in place rather than republished.
    /// </summary>
    private sealed class OperationContext
    {
        private readonly Lock @lock = new();
        private TagList meterTags;
        private bool errorTypeRecorded;

        public OperationContext(long beginTimestamp, Activity? activity)
        {
            this.BeginTimestamp = beginTimestamp;
            this.Activity = activity;
        }

        /// <summary>
        /// Gets the timestamp when the operation began. Used to compute duration if the <see cref="Activity"/>
        /// is not available (i.e. in a metrics-only scenario).
        /// </summary>
        public long BeginTimestamp { get; }

        /// <summary>
        /// Gets the current activity associated with the instance, if any.
        /// </summary>
        /// <remarks>
        /// Will be <see langword="null"/> in a metrics-only scenario.
        /// </remarks>
        public Activity? Activity { get; }

        public void AddMeterTag(string key, object? value)
        {
            lock (this.@lock)
            {
                this.meterTags.Add(key, value);
            }
        }

        public void RecordErrorType(string errorType)
        {
            // Tag error.type on both the span and the duration metric, in place and only once, so a duplicate
            // exception record cannot tag twice or overwrite the span tag with a later value.
            lock (this.@lock)
            {
                if (this.errorTypeRecorded)
                {
                    return;
                }

                this.Activity?.SetTag(SemanticConventions.AttributeErrorType, errorType);
                this.meterTags.Add(SemanticConventions.AttributeErrorType, errorType);
                this.errorTypeRecorded = true;
            }
        }

        public TagList GetMeterTags()
        {
            lock (this.@lock)
            {
                return this.meterTags;
            }
        }
    }
}
