// <copyright file="EventCountersInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Instrumentation-EventCounters")]
    internal class EventCountersInstrumentationEventSource : EventSource
    {
        public static readonly EventCountersInstrumentationEventSource Log = new EventCountersInstrumentationEventSource();

        [Event(1, Message = "Error occurred while processing eventCounter, EventCounter: {0}, Exception: {2}", Level = EventLevel.Error)]
        public void ErrorEventCounter(string counterName, string exception)
        {
            this.WriteEvent(1, counterName, exception);
        }

        [Event(2, Level = EventLevel.Informational, Message = @"EventCounterListener initialized successfully.")]
        public void EventCounterInitializeSuccess()
        {
            this.WriteEvent(2);
        }

        [Event(3, Level = EventLevel.Error, Message = @"EventCountersInstrumentation - {0} failed with exception: {1}.")]
        public void EventCountersInstrumentationError(string stage, string exceptionMessage)
        {
            this.WriteEvent(3, stage, exceptionMessage);
        }

        [Event(4, Level = EventLevel.Warning, Message = @"Ignoring event written from EventSource: {0} as payload is not IDictionary to extract metrics.")]
        public void IgnoreEventWrittenAsEventPayloadNotParseable(string eventSourceName)
        {
            this.WriteEvent(4, eventSourceName);
        }

        [Event(5, Level = EventLevel.Informational, Message = @"Ignoring event written from EventSource: {0} Counter: {1} as this counter is not configured to be collected.")]
        public void IgnoreEventWrittenAsCounterNotInConfiguredList(string eventSourceName, string counterName)
        {
            this.WriteEvent(5, eventSourceName, counterName);
        }

        [Event(6, Level = EventLevel.Warning, Message = @"EventCounter actual interval of {0} secs is less than configured interval of {1} secs.")]
        public void EventCounterRefreshIntervalLessThanConfigured(double actualInterval, int configuredInterval)
        {
            this.WriteEvent(6, actualInterval, configuredInterval);
        }

        [Event(7, Level = EventLevel.Warning, Message = @"EventCounter IntervalSec is 0. Using default interval. Counter Name: {0}.")]
        public void EventCounterIntervalZero(string counterName)
        {
            this.WriteEvent(7, counterName);
        }

        [Event(8, Level = EventLevel.Warning, Message = @"EventCountersInstrumentation - {0} failed with exception: {1}.")]
        public void EventCountersInstrumentationWarning(string stage, string exceptionMessage)
        {
            this.WriteEvent(8, stage, exceptionMessage);
        }

        [Event(9, Level = EventLevel.Informational, Message = @"Ignoring event written from EventSource: {0} as no counters from this event source are configured to be collected.")]
        public void IgnoreEventWrittenAsEventSourceNotInConfiguredList(string eventSourceName)
        {
            this.WriteEvent(9, eventSourceName);
        }
    }
}
