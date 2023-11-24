// <copyright file="ServiceConnectInstrumentationEventSource.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ServiceConnect.Implementation;

[EventSource(Name = "OpenTelemetry-Instrumentation-ServiceConnect")]
internal class ServiceConnectInstrumentationEventSource : EventSource
{
    public static readonly ServiceConnectInstrumentationEventSource Log = new();

    [NonEvent]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.UnknownErrorProcessingEvent(handlerName, eventName, ex.ToString());
        }
    }

    [NonEvent]
    public void EnrichmentException(string eventName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.EnrichmentException(eventName, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Unknown error processing event '{1}' from handler '{0}', Exception: {2}", Level = EventLevel.Error)]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, string ex)
    {
        this.WriteEvent(1, handlerName, eventName, ex);
    }

    [Event(2, Message = "Current Activity is NULL the '{0}' callback. Span will not be recorded.", Level = EventLevel.Warning)]
    public void NullActivity(string eventName)
    {
        this.WriteEvent(2, eventName);
    }

    [Event(3, Message = "Enrichment threw exception. Exception {0}.", Level = EventLevel.Error)]
    public void EnrichmentException(string eventName, string exception)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.WriteEvent(3, eventName, exception);
        }
    }
}
