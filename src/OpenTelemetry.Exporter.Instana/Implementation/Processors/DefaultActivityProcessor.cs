// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Instana.Implementation.Processors;

internal class DefaultActivityProcessor : ActivityProcessorBase, IActivityProcessor
{
    public override async Task ProcessAsync(Activity activity, InstanaSpan instanaSpan)
    {
        this.PreProcess(activity, instanaSpan);

        instanaSpan.N = InstanaExporterConstants.OTEL_SPAN_TYPE;

        string traceId = activity.TraceId.ToHexString();
        if (traceId.Length == 32)
        {
            instanaSpan.T = traceId.Substring(16);
            instanaSpan.Lt = traceId;
        }
        else
        {
            instanaSpan.T = traceId;
        }

        bool hasParent = false;
        string parentSpanId = activity.ParentSpanId.ToHexString();
        if (!string.IsNullOrEmpty(parentSpanId) && GetLongFromHex(parentSpanId) != 0)
        {
            hasParent = true;
            instanaSpan.P = parentSpanId;
        }

        instanaSpan.S = activity.SpanId.ToHexString();
        instanaSpan.K = GetSpanKind(activity.Kind);

        instanaSpan.Ts = activity.StartTimeUtc.Ticks;
        instanaSpan.D = activity.Duration.Ticks;

        SetKind(activity, instanaSpan);

        if (hasParent && instanaSpan.TransformInfo != null && instanaSpan.TransformInfo.IsEntrySpan)
        {
            // If an OTel entry span continues an ongoing trace (which is equivalent to the original span having a parent), it
            // always uses the IDs from the traceparent header, thus we mark the span with span.tp accordingly.
            instanaSpan.Tp = true;
        }

        await base.ProcessAsync(activity, instanaSpan).ConfigureAwait(false);
    }

    private static SpanKind GetSpanKind(ActivityKind activityKind)
    {
        switch (activityKind)
        {
            case ActivityKind.Consumer:
            case ActivityKind.Server:
                return SpanKind.ENTRY;
            case ActivityKind.Client:
            case ActivityKind.Producer:
                return SpanKind.EXIT;
            case ActivityKind.Internal:
                return SpanKind.INTERMEDIATE;
            default:
                return SpanKind.NOT_SET;
        }
    }

    private static long GetLongFromHex(string hexValue)
    {
        if (!string.IsNullOrEmpty(hexValue))
        {
            try
            {
                string[] ids = hexValue.Split(',');
                return Convert.ToInt64(ids[ids.Length - 1].Trim(), 16);
            }
            catch (Exception)
            {
            }
        }

        return 0;
    }

    private static void SetKind(Activity activity, InstanaSpan instanaSpan)
    {
        bool isEntrySpan = false;

        if (instanaSpan.Data?.data != null)
        {
            switch (activity.Kind)
            {
                case ActivityKind.Server:
                    isEntrySpan = true;
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.SERVER_KIND;
                    break;
                case ActivityKind.Client:
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.CLIENT_KIND;
                    break;
                case ActivityKind.Producer:
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.PRODUCER_KIND;
                    break;
                case ActivityKind.Consumer:
                    isEntrySpan = true;
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.CONSUMER_KIND;
                    break;
                case ActivityKind.Internal:
                    instanaSpan.Data.data[InstanaExporterConstants.KIND_FIELD] = InstanaExporterConstants.INTERNAL_KIND;
                    break;
                default:
                    break;
            }
        }

        if (instanaSpan.TransformInfo != null)
        {
            instanaSpan.TransformInfo.IsEntrySpan = isEntrySpan;
        }
    }
}
