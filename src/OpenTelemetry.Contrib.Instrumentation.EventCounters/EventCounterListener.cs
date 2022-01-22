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
using System.Linq;
using System.Reflection;
using OpenTelemetry.Contrib.Instrumentation.EventCounters.EventPipe;
using OpenTelemetry.Metrics;

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

        private readonly Meter meter;
        private readonly EventCounterListenerOptions options;
        private readonly ConcurrentDictionary<MetricKey, double> metricStore = new();
        private readonly List<EventSource> earlyCreatedEventSources = new List<EventSource>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventCounterListener"/> class.
        /// </summary>
        /// <param name="options">Options to configure the EventCounterListener.</param>
        public EventCounterListener(EventCounterListenerOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);

            // enable event sources which are already created
            foreach (var eventSource in this.earlyCreatedEventSources)
            {
                this.EnableEventSource(eventSource);
            }

            this.earlyCreatedEventSources.Clear();
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
                if (this.IsValidEvent(eventData))
                {
                    this.ExtractAndRecordMetric(eventData);
                }
            }
            catch (Exception ex)
            {
                EventCountersInstrumentationEventSource.Log.ErrorEventCounter(eventData.EventName, ex.ToString());
            }
        }

        private bool IsValidEvent(EventWrittenEventArgs eventData)
        {
            var provider = this.options.Providers.Single(provider => provider.ProviderName.Equals(eventData.EventSource.Name, StringComparison.OrdinalIgnoreCase));

            if (provider.CounterNames.Count == 0)
            {
                return true;
            }

            return provider.CounterNames.Contains(eventData.EventName, StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource == null)
            {
                throw new ArgumentNullException(nameof(eventSource));
            }

            // options might be null when this method gets called
            // before our constructor was completed
            if (this.options == null)
            {
                this.earlyCreatedEventSources.Add(eventSource);
            }
            else
            {
                this.EnableEventSource(eventSource);
            }
        }

        private void EnableEventSource(EventSource eventSource)
        {
            if (this.options.HasProvider(eventSource.Name))
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
            return this.metricStore[key];
        }

        private void ExtractAndRecordMetric(EventWrittenEventArgs eventWrittenEventArgs)
        {
            eventWrittenEventArgs.TryGetCounterPayload(out var eventPayload);

            if (eventPayload == null)
            {
                return;
            }

            var metricKey = new MetricKey(eventPayload);
            if (!this.metricStore.ContainsKey(metricKey))
            {
                this.meter.CreateObservableGauge<double>(eventPayload.Name, () => this.ObserveValue(metricKey), eventPayload.DisplayName);
            }

            this.metricStore[metricKey] = eventPayload.Value;
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
