using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounters.Implementation
{
    [DebuggerDisplay("{Name} ({ProviderName})")]
    internal class MetricTelemetry
    {
        /// <summary>
        /// Gets or sets the name of the metric.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the provider of the metric.
        /// </summary>
        public string ProviderName { get; set; }

        /// <summary>
        /// Gets a dictionary of application-defined property names and values providing additional information about this metric.
        /// </summary>
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets sum of the values of the metric samples.
        /// </summary>
        public double Sum { get; set; }

        /// <summary>
        /// Gets or sets the number of values in the sample set.
        /// </summary>
        public int? Count { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the counter type.
        /// </summary>
        public CounterType CounterType { get; set; } = CounterType.Metric;
    }
}
