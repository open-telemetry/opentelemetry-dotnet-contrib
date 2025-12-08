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

    [Event(2, Message = "Trace record payload is NULL or has NULL message, record will not be processed.", Level = EventLevel.Warning)]
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

    [Event(4, Message = "Unknown error processing trace record, Exception: {0}", Level = EventLevel.Error)]
    public void UnknownErrorProcessingTraceRecord(string exception) => this.WriteEvent(4, exception);
}
