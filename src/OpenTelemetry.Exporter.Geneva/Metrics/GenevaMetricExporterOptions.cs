// <copyright file="GenevaMetricExporterOptions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Geneva;

public class GenevaMetricExporterOptions
{
    private IReadOnlyDictionary<string, object> _prepopulatedMetricDimensions;
    private int _metricExporterIntervalMilliseconds = 60000;

    /// <summary>
    /// Gets or sets the ConnectionString which contains semicolon separated list of key-value pairs.
    /// For e.g.: "Account=OTelMonitoringAccount;Namespace=OTelMetricNamespace".
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the metric export interval in milliseconds. The default value is 60000.
    /// </summary>
    public int MetricExportIntervalMilliseconds
    {
        get
        {
            return this._metricExporterIntervalMilliseconds;
        }

        set
        {
            Guard.ThrowIfOutOfRange(value, min: 1000);

            this._metricExporterIntervalMilliseconds = value;
        }
    }

    /// <summary>
    /// Gets or sets the pre-populated dimensions for all the metrics exported by the exporter.
    /// </summary>
    public IReadOnlyDictionary<string, object> PrepopulatedMetricDimensions
    {
        get
        {
            return this._prepopulatedMetricDimensions;
        }

        set
        {
            Guard.ThrowIfNull(value);

            var copy = new Dictionary<string, object>(value.Count);

            foreach (var entry in value)
            {
                if (entry.Key.Equals(GenevaMetricExporter.DimensionKeyForCustomMonitoringAccount, StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals(GenevaMetricExporter.DimensionKeyForCustomMetricsNamespace, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"The dimension: {entry.Key} is reserved and cannot be used as a prepopulated dimension.");
                }

                if (entry.Key.Length > GenevaMetricExporter.MaxDimensionNameSize)
                {
                    throw new ArgumentException($"The dimension: {entry.Key} exceeds the maximum allowed limit of {GenevaMetricExporter.MaxDimensionNameSize} characters for a dimension name.");
                }

                if (entry.Value == null)
                {
                    throw new ArgumentNullException($"{nameof(this.PrepopulatedMetricDimensions)}[\"{entry.Key}\"]");
                }

                var dimensionValue = Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
                if (dimensionValue.Length > GenevaMetricExporter.MaxDimensionValueSize)
                {
                    throw new ArgumentException($"Value provided for the dimension: {entry.Key} exceeds the maximum allowed limit of {GenevaMetricExporter.MaxDimensionValueSize} characters for dimension value.");
                }

                copy[entry.Key] = entry.Value; // shallow copy
            }

            this._prepopulatedMetricDimensions = copy;
        }
    }
}
