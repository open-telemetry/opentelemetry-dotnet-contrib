// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

[EventSource(Name = "OpenTelemetry-Instrumentation-ConfluentKafka")]
internal sealed class ConfluentKafkaInstrumentationEventSource : EventSource
{
    public static ConfluentKafkaInstrumentationEventSource Log = new();

    private const int EventIdFailedToFetchClusterId = 1;
    private const int EventIdStatisticsHandlerFailed = 2;

    [NonEvent]
    public void StatisticsHandlerFailed(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.StatisticsHandlerFailed(ex.ToString());
        }
    }

    [Event(EventIdStatisticsHandlerFailed, Message = "Kafka statistics handler failed. Exception: '{0}'", Level = EventLevel.Warning)]
    public void StatisticsHandlerFailed(string exception)
    {
        this.WriteEvent(EventIdStatisticsHandlerFailed, exception);
    }

    [NonEvent]
    public void FailedToFetchClusterId(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToFetchClusterId(ex.ToString());
        }
    }

    [Event(EventIdFailedToFetchClusterId, Message = "Failed to fetch Kafka cluster id. Exception: '{0}'", Level = EventLevel.Warning)]
    public void FailedToFetchClusterId(string exception)
    {
        this.WriteEvent(EventIdFailedToFetchClusterId, exception);
    }
}
