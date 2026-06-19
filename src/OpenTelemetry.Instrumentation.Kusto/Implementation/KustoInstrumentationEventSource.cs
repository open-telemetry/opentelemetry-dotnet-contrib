// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// EventSource for Kusto instrumentation.
/// </summary>
[EventSource(Name = "OpenTelemetry-Instrumentation-Kusto")]
internal sealed class KustoInstrumentationEventSource : EventSource
{
    private const string TraceRequestBodyEnvironmentVariable = "KUSTO_DATA_TRACE_REQUEST_BODY";

    public static KustoInstrumentationEventSource Log { get; } = new();

    [NonEvent]
    public void EnrichmentException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.EnrichmentException(ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Enrichment exception: {0}", Level = EventLevel.Error)]
    public void EnrichmentException(string exception) => this.WriteEvent(1, exception);

    [Event(2, Message = "Trace record payload is null or has a null message, record will not be processed.", Level = EventLevel.Warning)]
    public void NullPayload() => this.WriteEvent(2);

    [Event(3, Message = "Failed to find context for activity ID '{0}', operation data will not be recorded.", Level = EventLevel.Warning)]
    public void ContextNotFound(string activityId) => this.WriteEvent(3, activityId);

    [NonEvent]
    public void UnknownErrorProcessingTraceRecord(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.UnknownErrorProcessingTraceRecord(ex.ToInvariantString());
        }
    }

    [Event(4, Message = "Unknown error processing trace record. Exception: {0}", Level = EventLevel.Error)]
    public void UnknownErrorProcessingTraceRecord(string exception) => this.WriteEvent(4, exception);

    [NonEvent]
    public void WarnIfQueryTextCaptureNotEnabled(bool recordQueryText, bool recordQuerySummary)
    {
        if ((recordQueryText || recordQuerySummary)
            && Environment.GetEnvironmentVariable(TraceRequestBodyEnvironmentVariable) != "1")
        {
            this.QueryTextCaptureNotEnabled();
        }
    }

    [Event(5, Message = $"Query text or summary recording is enabled, but the '{TraceRequestBodyEnvironmentVariable}' environment variable is not set to '1', so the Kusto client will not emit the query text and none will be recorded.", Level = EventLevel.Warning)]
    public void QueryTextCaptureNotEnabled() => this.WriteEvent(5);
}
