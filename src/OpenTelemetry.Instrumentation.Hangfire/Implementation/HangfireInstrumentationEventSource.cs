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

    [NonEvent]
    public void RecordJobExceptionFailed(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.RecordJobExceptionFailed(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void StartJobActivityFailed(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.StartJobActivityFailed(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void DisplayNameFuncFailed(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.DisplayNameFuncFailed(ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Enrich threw exception. Exception {0}.", Level = EventLevel.Warning)]
    public void EnrichmentException(string exception) => this.WriteEvent(1, exception);

    [Event(2, Message = "Custom state '{0}' maps to '{1}', which is not a built-in Hangfire state.", Level = EventLevel.Warning)]
    public void UnknownMappedState(string customState, string mappedState) => this.WriteEvent(2, customState, mappedState);

    [Event(3, Message = "Recording the job exception on the activity threw exception. Exception {0}.", Level = EventLevel.Warning)]
    public void RecordJobExceptionFailed(string exception) => this.WriteEvent(3, exception);

    [Event(4, Message = "Starting the job activity threw exception, the job runs untraced. Exception {0}.", Level = EventLevel.Warning)]
    public void StartJobActivityFailed(string exception) => this.WriteEvent(4, exception);

    [Event(5, Message = "DisplayNameFunc threw exception, the default display name is used. Exception {0}.", Level = EventLevel.Warning)]
    public void DisplayNameFuncFailed(string exception) => this.WriteEvent(5, exception);
}
