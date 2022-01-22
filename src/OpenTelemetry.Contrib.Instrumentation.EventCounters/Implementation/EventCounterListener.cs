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
using System.Globalization;
using System.Linq;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation
{
    /// <summary>
    /// EventCounterListener that subscribes to EventSource Events.
    /// </summary>
    internal class EventCounterListener : EventListener
    {
        private readonly EventCountersOptions options;
        private readonly ConcurrentQueue<EventSource> allEventSourcesCreated = new ConcurrentQueue<EventSource>();
        private readonly Dictionary<string, string> refreshIntervalDictionary;
        private readonly bool isInitialized = false;

        // EventSourceNames from which counters are to be collected are the keys for this IDictionary.
        // The value will be the corresponding ICollection of counter names.
        private readonly IDictionary<string, ICollection<string>> countersToCollect = new Dictionary<string, ICollection<string>>();
        private readonly IEventSourceTelemetryPublisher telemetryPublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventCounterListener"/> class.
        /// </summary>
        /// <param name="options">Options to configure the EventCounterListener.</param>
        /// <param name="telemetryPublisher">Instance to publish the retrieved telemetry data.</param>
        public EventCounterListener(EventCountersOptions options, IEventSourceTelemetryPublisher telemetryPublisher)
        {
            this.telemetryPublisher = telemetryPublisher ?? throw new ArgumentNullException(nameof(telemetryPublisher));

            try
            {
                this.options = options ?? throw new ArgumentNullException(nameof(options));

                this.refreshIntervalDictionary = new Dictionary<string, string>();
                this.refreshIntervalDictionary.Add("EventCounterIntervalSec", options.RefreshIntervalSecs.ToString());

                foreach (var source in options.Sources)
                {
                    this.countersToCollect.Add(source.EventSourceName, source.EventCounters.Select(c => c.Name).ToArray());
                }

                EventCountersInstrumentationEventSource.Log.EventCounterInitializeSuccess();
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
                EventCountersInstrumentationEventSource.Log.EventCountersInstrumentationError("EventCounterListener Constructor", ex.Message);
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
                if (this.countersToCollect.ContainsKey(eventData.EventSource.Name))
                {
                    if (eventData.Payload[0] is IDictionary<string, object> eventPayload)
                    {
                        this.ExtractAndPostMetric(eventData.EventSource.Name, eventPayload);
                    }
                    else
                    {
                        EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenAsEventPayloadNotParseable(eventData.EventSource.Name);
                    }
                }
                else
                {
                    EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenAsEventSourceNotInConfiguredList(eventData.EventSource.Name);
                }
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.ErrorEventCounter(eventData.EventName, ex.ToString());
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
                if (this.countersToCollect.ContainsKey(eventSource.Name))
                {
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, this.refreshIntervalDictionary);
                }
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.ErrorEventCounter(eventSource.Name, ex.Message);
            }
        }

        private void ExtractAndPostMetric(string eventSourceName, IDictionary<string, object> eventPayload)
        {
            try
            {
                MetricTelemetry metricTelemetry = new MetricTelemetry();
                bool calculateRate = false;
                double actualValue = 0.0;
                double actualInterval = 0.0;
                int actualCount = 0;
                string counterName = string.Empty;
                string counterDisplayName = string.Empty;
                string counterDisplayUnit = string.Empty;
                foreach (KeyValuePair<string, object> payload in eventPayload)
                {
                    var key = payload.Key;
                    if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        counterName = payload.Value.ToString();
                        var counterNames = this.countersToCollect[eventSourceName];
                        if (counterNames.Count > 0 && !counterNames.Contains(counterName))
                        {
                            EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenAsCounterNotInConfiguredList(eventSourceName, counterName);
                            return;
                        }
                    }
                    else if (key.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
                    {
                        counterDisplayName = payload.Value.ToString();
                    }
                    else if (key.Equals("DisplayUnits", StringComparison.OrdinalIgnoreCase))
                    {
                        counterDisplayUnit = payload.Value.ToString();
                    }
                    else if (key.Equals("Mean", StringComparison.OrdinalIgnoreCase))
                    {
                        actualValue = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                    }
                    else if (key.Equals("Increment", StringComparison.OrdinalIgnoreCase))
                    {
                        // Increment indicates we have to calculate rate.
                        actualValue = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                        calculateRate = true;
                    }
                    else if (key.Equals("IntervalSec", StringComparison.OrdinalIgnoreCase))
                    {
                        // Even though we configure 60 sec, we parse the actual duration from here. It'll be very close to the configured interval of 60.
                        // If for some reason this value is 0, then we default to 60 sec.
                        actualInterval = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                        if (actualInterval < this.options.RefreshIntervalSecs)
                        {
                            EventCountersInstrumentationEventSource.Log.EventCounterRefreshIntervalLessThanConfigured(actualInterval, this.options.RefreshIntervalSecs);
                        }
                    }
                    else if (key.Equals("Count", StringComparison.OrdinalIgnoreCase))
                    {
                        actualCount = Convert.ToInt32(payload.Value, CultureInfo.InvariantCulture);
                    }
                    else if (key.Equals("CounterType", StringComparison.OrdinalIgnoreCase))
                    {
                        if (payload.Value.Equals("Sum"))
                        {
                            metricTelemetry.Type = MetricType.Rate;
                            if (string.IsNullOrEmpty(counterDisplayUnit))
                            {
                                counterDisplayUnit = "count";
                            }
                        }
                    }
                    else if (key.Equals("Metadata", StringComparison.OrdinalIgnoreCase))
                    {
                        var metadata = payload.Value.ToString();
                        if (!string.IsNullOrEmpty(metadata))
                        {
                            var keyValuePairStrings = metadata.Split(',');
                            foreach (var keyValuePairString in keyValuePairStrings)
                            {
                                var keyValuePair = keyValuePairString.Split(':');
                                if (!metricTelemetry.Properties.ContainsKey(keyValuePair[0]))
                                {
                                    metricTelemetry.Properties.Add(keyValuePair[0], keyValuePair[1]);
                                }
                            }
                        }
                    }
                }

                if (calculateRate)
                {
                    if (actualInterval > 0)
                    {
                        metricTelemetry.Sum = actualValue / actualInterval;
                    }
                    else
                    {
                        metricTelemetry.Sum = actualValue / this.options.RefreshIntervalSecs;
                        EventCountersInstrumentationEventSource.Log.EventCounterIntervalZero(metricTelemetry.Name);
                    }
                }
                else
                {
                    metricTelemetry.Sum = actualValue;
                }

                metricTelemetry.Name = counterName;
                metricTelemetry.DisplayName = string.IsNullOrEmpty(counterDisplayName) ? counterName : counterDisplayName;
                metricTelemetry.EventSource = eventSourceName;

                if (!string.IsNullOrEmpty(counterDisplayUnit))
                {
                    metricTelemetry.Properties.Add("DisplayUnits", counterDisplayUnit);
                }

                metricTelemetry.Count = actualCount;
                this.telemetryPublisher.Publish(metricTelemetry);
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.EventCountersInstrumentationWarning("ExtractMetric", ex.Message);
            }
        }
    }
}
