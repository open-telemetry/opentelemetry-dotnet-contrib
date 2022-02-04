// <copyright file="MeterTelemetryPublisher.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation
{
    internal class MeterTelemetryPublisher : IEventSourceTelemetryPublisher
    {
        internal static readonly AssemblyName AssemblyName = typeof(EventCounterListener).Assembly.GetName();
        internal static readonly string InstrumentationName = AssemblyName.Name;
        internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

        private readonly Meter meter;
        private readonly ConcurrentDictionary<MetricKey, MetricInstrument> metricStore = new();
        private readonly ConcurrentDictionary<MetricKey, long> longValueStore = new();
        private readonly ConcurrentDictionary<MetricKey, double> doubleValueStore = new();
        private readonly EventCountersOptions options;

        public MeterTelemetryPublisher(EventCountersOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.options = options ?? throw new System.ArgumentNullException(nameof(options));
        }

        public void Publish(MetricTelemetry metricTelemetry)
        {
            var metricKey = new MetricKey(metricTelemetry);

            if (!this.metricStore.TryGetValue(metricKey, out var instrument))
            {
                instrument = this.CreateInstrument(metricTelemetry, metricKey);
            }

            this.StoreValue(metricKey, instrument, metricTelemetry);
        }

        private void StoreValue(MetricKey metricKey, MetricInstrument instrument, MetricTelemetry metricTelemetry)
        {
            switch (instrument.Type)
            {
                case InstrumentationType.LongCounter:
                case InstrumentationType.LongGauge:
                    this.longValueStore[metricKey] = (long)metricTelemetry.Sum;
                    break;
                case InstrumentationType.DoubleGauge:
                case InstrumentationType.DoubleCounter:
                    this.doubleValueStore[metricKey] = metricTelemetry.Sum;
                    break;
                default:
                    throw new InvalidOperationException($"Instrumentation type '{instrument.Type}' is not supported.");
            }
        }

        private MetricInstrument CreateInstrument(MetricTelemetry metricTelemetry, MetricKey metricKey)
        {
            var eventSource = this.options.Sources.Single(source => source.EventSourceName.Equals(metricTelemetry.EventSource, StringComparison.OrdinalIgnoreCase));
            var eventCounter = eventSource.EventCounters.FirstOrDefault(counter => counter.Name.Equals(metricTelemetry.Name, StringComparison.OrdinalIgnoreCase));

            var description = !string.IsNullOrEmpty(eventCounter?.Description) ? eventCounter.Description : metricTelemetry.DisplayName;
            var instrumentationType = eventCounter?.Type ?? metricTelemetry.Type;
            var metricName = !string.IsNullOrEmpty(eventCounter?.MetricName) ? eventCounter.MetricName : metricTelemetry.Name;
            var metricInstrument = new MetricInstrument { Type = instrumentationType };

            switch (instrumentationType)
            {
                case InstrumentationType.DoubleGauge:
                    metricInstrument.Instrument = this.meter.CreateObservableGauge(metricName, () => this.ObserveDouble(metricKey), description: description);
                    break;
                case InstrumentationType.DoubleCounter:
                    metricInstrument.Instrument = this.meter.CreateObservableCounter(metricName, () => this.ObserveDouble(metricKey), description: description);
                    break;
                case InstrumentationType.LongGauge:
                    metricInstrument.Instrument = this.meter.CreateObservableGauge<long>(metricName, () => this.ObserveLong(metricKey), description: description);
                    break;
                case InstrumentationType.LongCounter:
                    metricInstrument.Instrument = this.meter.CreateObservableCounter<long>(metricName, () => this.ObserveLong(metricKey), description: description);
                    break;
                default:
                    throw new InvalidOperationException($"Instrumentation type '{instrumentationType}' is not supported.");
            }

            return metricInstrument;
        }

        private double ObserveDouble(MetricKey key)
        {
            // return last value
            return this.doubleValueStore[key];
        }

        private long ObserveLong(MetricKey key)
        {
            // return last value
            return this.longValueStore[key];
        }

        private sealed class MetricKey
        {
            private readonly MetricTelemetry metric;

            public MetricKey(MetricTelemetry metric)
            {
                this.metric = metric;
            }

            public override int GetHashCode() => (this.metric.EventSource, this.metric.Name).GetHashCode();

            public override bool Equals(object obj)
            {
                if (obj is MetricKey metricKey)
                {
                    return CompareMetrics(this.metric, metricKey.metric);
                }

                return false;
            }

            private static bool CompareMetrics(MetricTelemetry first, MetricTelemetry second)
            {
                return string.Equals(first.Name, second.Name);
            }
        }

        private sealed class MetricInstrument
        {
            public Instrument Instrument { get; set; }

            public InstrumentationType Type { get; set; }
        }
    }
}
