// <copyright file="RuntimeInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Instrumentation-Runtime")]
    internal class RuntimeInstrumentationEventSource : EventSource
    {
        public static readonly RuntimeInstrumentationEventSource Log = new RuntimeInstrumentationEventSource();

        [Event(1, Level = EventLevel.Informational, Message = @"EventListener initialized successfully.")]
        public void EventListenerInitializeSuccess()
        {
            this.WriteEvent(1);
        }

        [Event(2, Level = EventLevel.Error, Message = @"EventListener - {0} failed with exception: {1}.")]
        public void EventListenerError(string stage, string exceptionMessage)
        {
            this.WriteEvent(2, stage, exceptionMessage);
        }

        [Event(3, Message = "Error occurred while processing eventCounter, EventCounter: {0}, Exception: {2}", Level = EventLevel.Error)]
        public void ErrorEventCounter(string counterName, string exception)
        {
            this.WriteEvent(3, counterName, exception);
        }

        [Event(4, Level = EventLevel.Warning, Message = @"Ignoring event written from EventCounter: {0} as payload is not IDictionary to extract metrics.")]
        public void IgnoreEventWrittenAsEventPayloadNotParseable(string counterName)
        {
            this.WriteEvent(4, counterName);
        }

        [Event(5, Level = EventLevel.Informational, Message = @"Ignoring event written from Counter: {0} as this counter is not configured to be collected.")]
        public void IgnoreEventWrittenAsCounterNotInConfiguredList(string counterName)
        {
            this.WriteEvent(5, counterName);
        }

        [Event(6, Level = EventLevel.Warning, Message = @"Parsing event counter failed with exception: {0}.")]
        public void EventCountersInstrumentationWarning(string exceptionMessage)
        {
            this.WriteEvent(6, exceptionMessage);
        }
    }
}
