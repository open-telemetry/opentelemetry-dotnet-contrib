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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

public static class GenevaMetricExporterExtensions
{
    /// <summary>
    /// Adds <see cref="GenevaMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddGenevaMetricExporter(this MeterProviderBuilder builder)
        => AddGenevaMetricExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds <see cref="GenevaMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Exporter configuration options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddGenevaMetricExporter(this MeterProviderBuilder builder, Action<GenevaMetricExporterOptions> configure)
        => AddGenevaMetricExporter(builder, name: null, configure);

    /// <summary>
    /// Adds <see cref="GenevaMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Exporter configuration options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddGenevaMetricExporter(this MeterProviderBuilder builder, string name, Action<GenevaMetricExporterOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        return builder.AddReader(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<GenevaMetricExporterOptions>>().Get(name);

            return BuildGenevaMetricExporter(exporterOptions, configure);
        });
    }

    private static MetricReader BuildGenevaMetricExporter(GenevaMetricExporterOptions options, Action<GenevaMetricExporterOptions> configure = null)
    {
        configure?.Invoke(options);
        return new PeriodicExportingMetricReader(new GenevaMetricExporter(options), options.MetricExportIntervalMilliseconds)
        { TemporalityPreference = MetricReaderTemporalityPreference.Delta };
    }
}
