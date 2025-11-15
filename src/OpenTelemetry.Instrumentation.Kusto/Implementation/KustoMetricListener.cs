// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using KustoUtils = Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal sealed class KustoMetricListener : KustoUtils.ITraceListener
{
    private readonly KustoInstrumentationOptions options;
    private AsyncLocal<long> beginTimestamp = new();

    public KustoMetricListener(KustoInstrumentationOptions options)
    {
        this.options = options;
    }

    public override string Name => nameof(KustoMetricListener);

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
            this.HangleActivityComplete(record);
        }
    }

    private static double GetElaspedTime(long begin)
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

    private void HangleActivityComplete(KustoUtils.TraceRecord record)
    {
        var operationName = record.Activity.ActivityType;
        var duration = GetElaspedTime(this.beginTimestamp.Value);

        var tags = new TagList
        {
            { SemanticConventions.AttributeDbSystemName, KustoActivitySourceHelper.DbSystem },
            { SemanticConventions.AttributeDbOperationName, operationName },
        };

        KustoActivitySourceHelper.OperationDurationHistogram.Record(duration, tags);
        KustoActivitySourceHelper.OperationCounter.Add(1, tags);
    }

    private void HandleHttpRequestStart(KustoUtils.TraceRecord record) => this.beginTimestamp.Value = Stopwatch.GetTimestamp();
}
