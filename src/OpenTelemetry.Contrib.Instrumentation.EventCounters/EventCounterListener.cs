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
using OpenTelemetry.Contrib.Instrumentation.EventCounters.EventPipe;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters
{
    /// <summary>
    /// EventCounterListener that subscribes to EventSource Events.
    /// </summary>
    internal class EventCounterListener : EventListener
    {
        internal static readonly AssemblyName AssemblyName = typeof(EventCounterListener).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

        private readonly string eventSourceName = "System.Runtime"; // TODO : Get from options
        private readonly string eventName = "EventCounters";
        private readonly Meter meter;
        private readonly EventCounterListenerOptions options;

        private ConcurrentDictionary<MetricKey, double> metericStore = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventCounterListener"/> class.
        /// </summary>
        /// <param name="options">Options to configure the EventCounterListener</param>
        public EventCounterListener(EventCounterListenerOptions options)
        {
            this.options = options;
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
        }

        /// <summary>
        /// Processes a new EventSource event.
        /// </summary>
        /// <param name="eventData">Event to process.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            try
            {
                if (eventData.EventName.Equals(this.eventName, StringComparison.OrdinalIgnoreCase))
                {
                    this.ExtractAndRecordMetric(eventData);
                }
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.ErrorEventCounter(this.eventName, ex.ToString());
            }
        }

        /// <inheritdoc/>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource == null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            if (this.eventSourceName.Equals(eventSource.Name, StringComparison.OrdinalIgnoreCase))
            {
                var refreshInterval = new Dictionary<string, string>() { { "EventCounterIntervalSec", "1" } }; // TODO: Get from configuration
                try
                {
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, refreshInterval);
                }
                catch (Exception ex)
                {
                    // TODO: Log to eventSource
                }
            }
        }

        private static bool CompareMetrics(ICounterPayload first, ICounterPayload second)
        {
            return string.Equals(first.Name, second.Name);
        }

        private double ObserveValue(MetricKey key)
        {
            // return last value
            return this.metericStore[key];
        }

        private void ExtractAndRecordMetric(EventWrittenEventArgs eventWrittenEventArgs)
        {
            eventWrittenEventArgs.TryGetCounterPayload(out var eventPayload);
            var metricKey = new MetricKey(eventPayload);
            if (!this.metericStore.ContainsKey(metricKey))
            {
                this.meter.CreateObservableGauge<double>(eventPayload.Name, () => this.ObserveValue(metricKey), eventPayload.DisplayName);
            }

            this.metericStore[metricKey] = eventPayload.Value;
        }

        private sealed class MetricKey
        {
            private readonly ICounterPayload metric;

            public MetricKey(ICounterPayload metric)
            {
                this.metric = metric;
            }

            public override int GetHashCode() => (this.metric.Provider, this.metric.Name).GetHashCode();

            public override bool Equals(object obj)
            {
                if (obj is MetricKey metricKey)
                {
                    return CompareMetrics(this.metric, metricKey.metric);
                }

                return false;
            }
        }
    }
}
