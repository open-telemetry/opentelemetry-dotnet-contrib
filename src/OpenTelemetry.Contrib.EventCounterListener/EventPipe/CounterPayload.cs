using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTelemetry.Contrib.EventCounterListener.EventPipe
{
    internal class CounterPayload : ICounterPayload
    {
        public CounterPayload(DateTime timestamp,
            string provider,
            string name,
            string displayName,
            string unit,
            double value,
            CounterType counterType,
            float interval)
        {
            Timestamp = timestamp;
            Name = name;
            DisplayName = displayName;
            Unit = unit;
            Value = value;
            CounterType = counterType;
            Provider = provider;
            Interval = interval;
        }

        public string Namespace { get; }

        public string Name { get; }

        public string DisplayName { get; }

        public string Unit { get; }

        public double Value { get; }

        public DateTime Timestamp { get; }

        public float Interval { get; }

        public CounterType CounterType { get; }

        public string Provider { get; }
    }
}
