// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AWS;

[EventSource(Name = "OpenTelemetry-Instrumentation-AWS")]
internal sealed class AWSInstrumentationEventSource : EventSource
{
    public static AWSInstrumentationEventSource Log = new();

    private const int EventIdFailedToParseJson = 1;

    [NonEvent]
    public void JsonParserException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedToPraseJson(format, ex.ToInvariantString());
        }
    }

    [Event(EventIdFailedToParseJson, Message = "Failed to parse Json in {0}. Error Message: '{1}'", Level = EventLevel.Warning)]
    public void FailedToPraseJson(string format, string exception)
    {
        this.WriteEvent(EventIdFailedToParseJson, format, exception);
    }
}
