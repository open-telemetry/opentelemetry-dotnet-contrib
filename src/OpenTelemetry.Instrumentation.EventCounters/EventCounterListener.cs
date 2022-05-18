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
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Reflection;

namespace OpenTelemetry.Instrumentation.EventCounters
{
    internal class EventCounterListener : EventListener
    {
        // EventSourceCreated can run before constructor, so constructor injection of the options dependency will not work.
        // Using static to set options at host build time.
        public static EventCounterMetricsOptions EventCounterMetricsOptions = new EventCounterMetricsOptions();

        internal static readonly AssemblyName AssemblyName = typeof(EventCounterListener).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();
        private readonly bool isInitialized = false;
        private readonly Meter meter;
        private readonly ConcurrentDictionary<MetricKey, Instrument> metricInstruments = new();
        private readonly ConcurrentDictionary<MetricKey, long> lastLongValue = new();
        private readonly ConcurrentDictionary<MetricKey, double> lastDoubleValue = new();

        public EventCounterListener()
        {
           // this.options = options;
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);

            this.isInitialized = true;
        }

        private enum InstrumentType
        {
            Gauge,
            Counter,
        }

        protected override void OnEventSourceCreated(EventSource source)
        {
            // TODO: Add Configuration options to selectively subscribe to EventCounters

            try
            {
                var arguments = new Dictionary<string, string>
                {
                    ["EventCounterIntervalSec"] = EventCounterMetricsOptions.RefreshIntervalSecs.ToString(),
                };

                this.EnableEvents(source, EventLevel.Verbose, EventKeywords.All, arguments);
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.ErrorEventCounter(source.Name, ex.Message);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (!this.isInitialized || !eventData.EventName.Equals("EventCounters"))
            {
                return;
            }

            try
            {
                if (eventData.Payload.Count > 0 && eventData.Payload[0] is IDictionary<string, object> eventPayload)
                {
                    this.ExtractAndPostMetric(eventData.EventSource.Name, eventPayload);
                }
                else
                {
                    EventCountersInstrumentationEventSource.Log.IgnoreEventWrittenAsEventPayloadNotParseable(eventData.EventSource.Name);
                }
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.ErrorEventCounter(eventData.EventName, ex.ToString());
            }
        }

        private void ExtractAndPostMetric(string eventSourceName, IDictionary<string, object> eventPayload)
        {
            try
            {
                bool calculateRate = false;
                double actualValue = 0.0;
                double actualInterval = 0.0;
                double recordedValue = 0.0;

                string counterName = string.Empty;
                string counterDisplayName = string.Empty;
                InstrumentType instrumentType = InstrumentType.Counter;

                foreach (KeyValuePair<string, object> payload in eventPayload)
                {
                    var key = payload.Key;
                    if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        counterName = payload.Value.ToString();
                    }
                    else
                    if (key.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
                    {
                        counterDisplayName = payload.Value.ToString();
                    }
                    else if (key.Equals("Mean", StringComparison.OrdinalIgnoreCase))
                    {
                        instrumentType = InstrumentType.Counter;
                    }
                    else if (key.Equals("Increment", StringComparison.OrdinalIgnoreCase))
                    {
                        // Increment indicates we have to calculate rate.
                        instrumentType = InstrumentType.Gauge;
                        calculateRate = true;
                    }
                    else if (key.Equals("IntervalSec", StringComparison.OrdinalIgnoreCase))
                    {
                        // Even though we configure 60 sec, we parse the actual duration from here. It'll be very close to the configured interval of 60.
                        // If for some reason this value is 0, then we default to 60 sec.
                        actualInterval = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                        if (actualInterval < EventCounterMetricsOptions.RefreshIntervalSecs)
                        {
                            EventCountersInstrumentationEventSource.Log.EventCounterRefreshIntervalLessThanConfigured(actualInterval, EventCounterMetricsOptions.RefreshIntervalSecs);
                        }
                    }
                }

                if (calculateRate)
                {
                    if (actualInterval > 0)
                    {
                        recordedValue = actualValue / actualInterval;
                    }
                    else
                    {
                        recordedValue = actualValue / EventCounterMetricsOptions.RefreshIntervalSecs;
                        EventCountersInstrumentationEventSource.Log.EventCounterIntervalZero(counterName);
                    }
                }
                else
                {
                    recordedValue = actualValue;
                }

                this.RecordMetric(eventSourceName, counterName, counterDisplayName, instrumentType, recordedValue);
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.EventCountersInstrumentationWarning("ExtractMetric", ex.Message);
            }
        }

        private void RecordMetric(string eventSourceName, string counterName, string displayName, InstrumentType instrumentType, double recordedValue)
        {
            var metricKey = new MetricKey(eventSourceName, counterName);
            var description = string.IsNullOrEmpty(displayName) ? counterName : displayName;
            bool isLong = long.TryParse(recordedValue.ToString(), out long longValue);
            bool isDouble = double.TryParse(recordedValue.ToString(), out double doubleValue);

            if (isLong)
            {
                this.lastLongValue[metricKey] = longValue;
            }
            else if (isDouble)
            {
                this.lastDoubleValue[metricKey] = doubleValue;
            }

            switch (instrumentType)
            {
                case InstrumentType.Counter when isLong:

                    if (!this.metricInstruments.ContainsKey(metricKey))
                    {
                        this.metricInstruments[metricKey] = this.meter.CreateObservableCounter<long>(counterName, () => this.ObserveLong(metricKey), description: description);
                    }

                    break;

                case InstrumentType.Counter when isDouble:
                    if (!this.metricInstruments.ContainsKey(metricKey))
                    {
                        this.metricInstruments[metricKey] = this.meter.CreateObservableCounter<double>(counterName, () => this.ObserveDouble(metricKey), description: description);
                    }

                    break;

                case InstrumentType.Gauge when isLong:
                    if (!this.metricInstruments.ContainsKey(metricKey))
                    {
                        this.metricInstruments[metricKey] = this.meter.CreateObservableGauge<long>(counterName, () => this.ObserveLong(metricKey), description: description);
                    }

                    break;
                case InstrumentType.Gauge when isDouble:

                    if (!this.metricInstruments.TryGetValue(metricKey, out Instrument instrument))
                    {
                        this.metricInstruments[metricKey] = this.meter.CreateObservableGauge<double>(counterName, () => this.ObserveDouble(metricKey), description: description);
                    }

                    break;
            }
        }

        private long ObserveLong(MetricKey key) => this.lastLongValue[key];

        private double ObserveDouble(MetricKey key) => this.lastDoubleValue[key];

        private class MetricKey
        {
            public MetricKey(string eventSourceName, string counterName)
            {
                this.EventSourceName = eventSourceName;
                this.CounterName = counterName;
            }

            public string EventSourceName { get; private set; }

            public string CounterName { get; private set; }

            public override int GetHashCode() => (this.EventSourceName, this.CounterName).GetHashCode();

            public override bool Equals(object obj) =>
                obj is MetricKey nextKey && this.EventSourceName == nextKey.EventSourceName && this.CounterName == nextKey.CounterName;
        }
    }
}
