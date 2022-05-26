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
using System.Reflection;

namespace OpenTelemetry.Instrumentation.EventCounters
{
    internal class EventCounterListener : EventListener
    {
        internal static readonly AssemblyName AssemblyName = typeof(EventCounterListener).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

        private readonly bool isInitialized = false;
        private readonly Meter meter;
        private readonly EventCounterMetricsOptions options;
        private readonly ConcurrentDictionary<MetricKey, Instrument> metricInstruments = new();
        private readonly ConcurrentDictionary<MetricKey, long> lastLongValue = new();
        private readonly ConcurrentDictionary<MetricKey, double> lastDoubleValue = new();
        private readonly ConcurrentBag<EventSource> eventSources = new();

        public EventCounterListener(EventCounterMetricsOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.isInitialized = true;
            this.EnablePendingEventSources(); // Some OnEventSourceCreated may have fired before constructor, enable them
        }

        private enum InstrumentType
        {
            Gauge,
            Counter,
        }

        private Dictionary<string, string> EnableEventArgs => new Dictionary<string, string> { ["EventCounterIntervalSec"] = this.options.RefreshIntervalSecs.ToString(), };

        protected override void OnEventSourceCreated(EventSource source)
        {
            // TODO: Add Configuration options to selectively subscribe to EventCounters
            try
            {
                if (!this.isInitialized)
                {
                    this.eventSources.Add(source);
                }
                else
                {
                    this.EnableEvents(source, EventLevel.Verbose, EventKeywords.All, this.EnableEventArgs);
                }
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
                string actualValue = string.Empty;

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
                        actualValue = payload.Value.ToString();
                    }
                    else if (key.Equals("Increment", StringComparison.OrdinalIgnoreCase))
                    {
                        // Increment indicates we have to calculate rate.
                        instrumentType = InstrumentType.Gauge;
                        calculateRate = true;
                        actualValue = payload.Value.ToString();
                    }
                }

                this.RecordMetric(eventSourceName, counterName, counterDisplayName, instrumentType, actualValue, calculateRate);
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.EventCountersInstrumentationWarning("ExtractMetric", ex.Message);
            }
        }

        private void RecordMetric(string eventSourceName, string counterName, string displayName, InstrumentType instrumentType, string value, bool calculateRate)
        {
            var metricKey = new MetricKey(eventSourceName, counterName);
            var description = string.IsNullOrEmpty(displayName) ? counterName : displayName;
            bool isLong = long.TryParse(value, out long longValue);
            bool isDouble = double.TryParse(value, out double doubleValue);

            if (isLong)
            {
                this.lastLongValue[metricKey] = calculateRate ? longValue / this.options.RefreshIntervalSecs : longValue;
            }
            else if (isDouble)
            {
                this.lastDoubleValue[metricKey] = calculateRate ? doubleValue / this.options.RefreshIntervalSecs : doubleValue;
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

        private void EnablePendingEventSources()
        {
            foreach (var source in this.eventSources)
            {
                this.EnableEvents(source, EventLevel.Verbose, EventKeywords.All, this.EnableEventArgs);
            }
        }

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
