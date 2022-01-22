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
        private readonly EventCounterListenerOptions options;
        private readonly ConcurrentDictionary<string, string> metricsNameLookup = new ConcurrentDictionary<string, string>();

        public MeterTelemetryPublisher(EventCounterListenerOptions options)
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
            this.options = options ?? throw new System.ArgumentNullException(nameof(options));
        }

        public void Publish(MetricTelemetry metricTelemetry)
        {
            var metricKey = new MetricKey(metricTelemetry);
            if (!this.metricStore.ContainsKey(metricKey))
            {
                if (metricTelemetry.CounterType == CounterType.Rate)
                {
                    this.meter.CreateObservableGauge<double>(this.GetMetricName(metricTelemetry), () => this.ObserveValue(metricKey), description: metricTelemetry.DisplayName);
                }
                else
                {
                    this.meter.CreateObservableCounter(this.GetMetricName(metricTelemetry), () => this.ObserveValue(metricKey), description: metricTelemetry.DisplayName);
                }
            }

            this.metricStore[metricKey] = metricTelemetry.Sum;
        }

        private static bool CompareMetrics(MetricTelemetry first, MetricTelemetry second)
        {
            return string.Equals(first.Name, second.Name);
        }

        private string GetMetricName(MetricTelemetry metricTelemetry)
        {
            var key = $"{metricTelemetry.ProviderName}-{metricTelemetry.Name}";

            if (!this.metricsNameLookup.TryGetValue(key, out var metricName))
            {
                metricName = this.options.MetricNameMapper?.Invoke(metricTelemetry.ProviderName, metricTelemetry.Name);

                if (string.IsNullOrEmpty(metricName))
                {
                    metricName = metricTelemetry.Name;
                }

                this.metricsNameLookup.TryAdd(key, metricName);
            }

            return metricName;
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

            public override int GetHashCode() => (this.metric.ProviderName, this.metric.Name).GetHashCode();

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
