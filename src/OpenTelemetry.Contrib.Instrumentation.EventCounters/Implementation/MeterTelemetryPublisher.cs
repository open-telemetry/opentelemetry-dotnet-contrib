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

        public MeterTelemetryPublisher()
        {
            this.meter = new Meter(InstrumentationName, InstrumentationVersion);
        }

        public void Publish(MetricTelemetry metricTelemetry)
        {
            var metricKey = new MetricKey(metricTelemetry);
            if (!this.metricStore.ContainsKey(metricKey))
            {
                this.meter.CreateObservableGauge<double>(metricTelemetry.Name, () => this.ObserveValue(metricKey), metricTelemetry.DisplayName);
            }

            this.metricStore[metricKey] = metricTelemetry.Sum;
        }

        private static bool CompareMetrics(MetricTelemetry first, MetricTelemetry second)
        {
            return string.Equals(first.Name, second.Name);
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
