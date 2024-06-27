// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

    private static PeriodicExportingMetricReader BuildGenevaMetricExporter(GenevaMetricExporterOptions options, Action<GenevaMetricExporterOptions> configure = null)
    {
        configure?.Invoke(options);
        return new PeriodicExportingMetricReader(new GenevaMetricExporter(options), options.MetricExportIntervalMilliseconds)
        { TemporalityPreference = MetricReaderTemporalityPreference.Delta };
    }
}
