// <copyright file="GenevaMetricExporterExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

public static class GenevaMetricExporterExtensions
{
    /// <summary>
    /// Adds <see cref="GenevaMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Exporter configuration options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddGenevaMetricExporter(this MeterProviderBuilder builder, Action<GenevaMetricExporterOptions> configure = null)
    {
        Guard.ThrowIfNull(builder);

        if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            return deferredMeterProviderBuilder.Configure((sp, builder) =>
            {
                AddGenevaMetricExporter(builder, sp.GetOptions<GenevaMetricExporterOptions>(), configure);
            });
        }

        return AddGenevaMetricExporter(builder, new GenevaMetricExporterOptions(), configure);
    }

    private static MeterProviderBuilder AddGenevaMetricExporter(MeterProviderBuilder builder, GenevaMetricExporterOptions options, Action<GenevaMetricExporterOptions> configure = null)
    {
        configure?.Invoke(options);
        return builder.AddReader(new PeriodicExportingMetricReader(new GenevaMetricExporter(options), options.MetricExportIntervalMilliseconds)
        { TemporalityPreference = MetricReaderTemporalityPreference.Delta });
    }
}
