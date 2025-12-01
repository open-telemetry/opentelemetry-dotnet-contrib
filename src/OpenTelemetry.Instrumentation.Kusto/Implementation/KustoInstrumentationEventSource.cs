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
    public void EnrichmentException(string exception)
    {
        this.WriteEvent(1, exception);
    }
}
