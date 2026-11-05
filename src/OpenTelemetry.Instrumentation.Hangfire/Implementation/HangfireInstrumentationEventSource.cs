// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Hangfire.Implementation;

[EventSource(Name = "OpenTelemetry-Instrumentation-Hangfire")]
internal sealed class HangfireInstrumentationEventSource : EventSource
{
    public static readonly HangfireInstrumentationEventSource Log = new();

    [NonEvent]
    public void EnrichmentException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.EnrichmentException(ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Enrich threw exception. Exception {0}.", Level = EventLevel.Warning)]
    public void EnrichmentException(string exception) => this.WriteEvent(1, exception);

    [Event(2, Message = "Custom state '{0}' maps to '{1}', which is not a built-in Hangfire state.", Level = EventLevel.Warning)]
    public void UnknownMappedState(string customState, string mappedState) => this.WriteEvent(2, customState, mappedState);
}
