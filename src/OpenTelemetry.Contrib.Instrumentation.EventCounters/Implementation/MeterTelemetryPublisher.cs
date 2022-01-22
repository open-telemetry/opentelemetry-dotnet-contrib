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
        private readonly ConcurrentDictionary<MetricKey, double> metricStore = new();
        private readonly EventCountersOptions options;

        public MeterTelemetryPublisher(EventCountersOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.options = options ?? throw new System.ArgumentNullException(nameof(options));
        }

        public void Publish(MetricTelemetry metricTelemetry)
        {
            var metricKey = new MetricKey(metricTelemetry);
            if (!this.metricStore.ContainsKey(metricKey))
            {
                this.CreateInstrument(metricTelemetry, metricKey);
            }

            this.metricStore[metricKey] = metricTelemetry.Sum;
        }

        private static bool CompareMetrics(MetricTelemetry first, MetricTelemetry second)
        {
            return string.Equals(first.Name, second.Name);
        }

        private void CreateInstrument(MetricTelemetry metricTelemetry, MetricKey metricKey)
        {
            var eventSource = this.options.Sources.Single(source => source.EventSourceName.Equals(metricTelemetry.EventSource, System.StringComparison.OrdinalIgnoreCase));
            var eventCounter = eventSource.EventCounters.Single(counter => counter.Name.Equals(metricTelemetry.Name, System.StringComparison.OrdinalIgnoreCase));

            var description = !string.IsNullOrEmpty(eventCounter.Description) ? eventCounter.Description : metricTelemetry.DisplayName;
            var metricType = eventCounter.Type ?? metricTelemetry.Type;
            var metricName = !string.IsNullOrEmpty(eventCounter.MetricName) ? eventCounter.MetricName : eventCounter.Name;

            if (metricType == MetricType.Rate)
            {
                this.meter.CreateObservableGauge<double>(metricName, () => this.ObserveValue(metricKey), description: description);
            }
            else
            {
                this.meter.CreateObservableCounter(metricName, () => this.ObserveValue(metricKey), description: description);
            }
        }

        private double ObserveValue(MetricKey key)
        {
            // return last value
            return this.metricStore[key];
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
        }
    }
}
