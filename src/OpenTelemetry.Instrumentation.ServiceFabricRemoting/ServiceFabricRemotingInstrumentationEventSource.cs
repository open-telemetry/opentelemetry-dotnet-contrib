// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

[EventSource(Name = "OpenTelemetry-Instrumentation-ServiceFabricRemoting")]
internal sealed class ServiceFabricRemotingInstrumentationEventSource : EventSource
{
    public static readonly ServiceFabricRemotingInstrumentationEventSource Log = new();

    [NonEvent]
    public void EnrichmentException(string handlerName, string eventName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.EnrichmentException(handlerName, eventName, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Enrich threw exception. HandlerName: '{0}', EventName: '{1}', Exception: {2}.", Level = EventLevel.Warning)]
    public void EnrichmentException(string handlerName, string eventName, string exception)
    {
        this.WriteEvent(1, handlerName, eventName, exception);
    }
}
