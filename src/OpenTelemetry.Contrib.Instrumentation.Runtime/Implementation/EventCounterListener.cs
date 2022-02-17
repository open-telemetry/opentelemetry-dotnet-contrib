// <copyright file="EventCounterListener.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Contrib.Instrumentation.Runtime.Implementation
{
    /// <summary>
    /// EventCounterListener that subscribes to EventSource Events.
    /// </summary>
    internal class EventCounterListener : EventListener
    {
        private const string EVENTSOURCENAME = "System.Runtime";

        private readonly ConcurrentQueue<EventSource> allEventSourcesCreated = new ConcurrentQueue<EventSource>();
        private readonly Dictionary<string, string> refreshIntervalDictionary;
        private readonly bool isInitialized = false;

        private readonly IEventCounterStore eventCounterStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventCounterListener"/> class.
        /// </summary>
        /// <param name="eventCounterStore">Instance to store the data to.</param>
        public EventCounterListener(IEventCounterStore eventCounterStore)
        {
            this.eventCounterStore = eventCounterStore ?? throw new ArgumentNullException(nameof(eventCounterStore));

            try
            {
                this.refreshIntervalDictionary = new Dictionary<string, string>();
                this.refreshIntervalDictionary.Add("EventCounterIntervalSec", "1");

                RuntimeInstrumentationEventSource.Log.EventListenerInitializeSuccess();
                this.isInitialized = true;

                // Go over every EventSource created before we finished initialization, and enable if required.
                // This will take care of all EventSources created before initialization was done.
                foreach (var eventSource in this.allEventSourcesCreated)
                {
                    this.EnableEventSource(eventSource);
                }
            }
            catch (Exception ex)
            {
                RuntimeInstrumentationEventSource.Log.EventListenerError("EventCounterListener Constructor", ex.Message);
            }
        }

        /// <summary>
        /// Processes a new EventSource event.
        /// </summary>
        /// <param name="eventData">Event to process.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // Ignore events if initialization not done yet. We may lose the 1st event if it happens before initialization, in multi-thread situations.
            // Since these are counters, losing the 1st event will not have noticeable impact.
            if (!this.isInitialized)
            {
                return;
            }

            try
            {
                if (eventData.EventSource.Name == EVENTSOURCENAME)
                {
                    if (eventData.Payload.Count > 0 && eventData.Payload[0] is IDictionary<string, object> eventPayload)
                    {
                        this.ExtractValue(eventPayload);
                    }
                    else
                    {
                        RuntimeInstrumentationEventSource.Log.IgnoreEventWrittenAsEventPayloadNotParseable(eventData.EventName);
                    }
                }
            }
            catch (Exception ex)
            {
                RuntimeInstrumentationEventSource.Log.ErrorEventCounter(eventData.EventName, ex.ToString());
            }
        }

        /// <inheritdoc/>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Keeping track of all EventSources here, as this call may happen before initialization.
            this.allEventSourcesCreated.Enqueue(eventSource);

            // options might be null when this method gets called
            // before our constructor was completed
            if (this.isInitialized)
            {
                this.EnableEventSource(eventSource);
            }
        }

        private void EnableEventSource(EventSource eventSource)
        {
            try
            {
                if (eventSource.Name == EVENTSOURCENAME)
                {
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, this.refreshIntervalDictionary);
                }
            }
            catch (Exception ex)
            {
                RuntimeInstrumentationEventSource.Log.EventListenerError(eventSource.Name, ex.Message);
            }
        }

        private void ExtractValue(IDictionary<string, object> eventPayload)
        {
            try
            {
                if (eventPayload.TryGetValue("Name", out var p) && p is string counterName)
                {
                    if (!this.eventCounterStore.HasSubscription(counterName))
                    {
                        RuntimeInstrumentationEventSource.Log.IgnoreEventWrittenAsCounterNotInConfiguredList(counterName);
                        return;
                    }

                    this.eventCounterStore.WriteValue(counterName, eventPayload);
                }
            }
            catch (Exception ex)
            {
                RuntimeInstrumentationEventSource.Log.EventCountersInstrumentationWarning(ex.Message);
            }
        }
    }
}
