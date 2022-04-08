using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenTelemetry.Exporter.Geneva
{
    public class GenevaMetricExporterOptions
    {
        private IReadOnlyDictionary<string, object> _prepopulatedMetricDimensions;

        /// <summary>
        /// Gets or sets the ConnectionString which contains semicolon separated list of key-value pairs.
        /// For e.g.: "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace".
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the metric export interval in milliseconds. The default value is 20000.
        /// </summary>
        public int MetricExportIntervalMilliseconds { get; set; } = 20000;

        /// <summary>
        /// Gets or sets the pre-populated dimensions for all the metrics exported by the exporter
        /// </summary>
        public IReadOnlyDictionary<string, object> PrepopulatedMetricDimensions
        {
            get
            {
                return _prepopulatedMetricDimensions;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var copy = new Dictionary<string, object>(value.Count);

                foreach (var entry in value)
                {
                    if (entry.Key.Length > GenevaMetricExporter.MaxDimensionNameSize)
                    {
                        throw new ArgumentException($"The dimension: {entry.Key} exceeds the maximum allowed limit of {GenevaMetricExporter.MaxDimensionNameSize} characters for a dimension name.");
                    }

                    if (entry.Value == null)
                    {
                        throw new ArgumentNullException($"{nameof(PrepopulatedMetricDimensions)}[\"{entry.Key}\"]");
                    }

                    var dimensionValue = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
                    if (dimensionValue.Length > GenevaMetricExporter.MaxDimensionValueSize)
                    {
                        throw new ArgumentException($"Value provided for the dimension: {entry.Key} exceeds the maximum allowed limit of {GenevaMetricExporter.MaxDimensionValueSize} characters for dimension value.");
                    }

                    copy[entry.Key] = entry.Value; // shallow copy
                }

                _prepopulatedMetricDimensions = copy;
            }
        }
    }
}
