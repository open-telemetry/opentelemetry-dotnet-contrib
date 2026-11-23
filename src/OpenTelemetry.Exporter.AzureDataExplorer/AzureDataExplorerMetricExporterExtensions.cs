// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.AzureDataExplorer;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering the Azure Data Explorer metric exporter.
/// </summary>
public static class AzureDataExplorerMetricExporterExtensions
{
    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="MeterProviderBuilder"/>
    /// using a <see cref="PeriodicExportingMetricReader"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAzureDataExplorerExporter(this MeterProviderBuilder builder)
        => AddAzureDataExplorerExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="MeterProviderBuilder"/>
    /// using a <see cref="PeriodicExportingMetricReader"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="AzureDataExplorerExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAzureDataExplorerExporter(this MeterProviderBuilder builder, Action<AzureDataExplorerExporterOptions> configure)
        => AddAzureDataExplorerExporter(builder, name: null, configure);

    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="MeterProviderBuilder"/>
    /// using a <see cref="PeriodicExportingMetricReader"/>.
    /// </summary>
    /// <remarks>
    /// The reader is configured from <see cref="MetricReaderOptions"/>
    /// (optionally named with <paramref name="name"/>).
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="AzureDataExplorerExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAzureDataExplorerExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<AzureDataExplorerExporterOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        builder.ConfigureServices(services =>
            AzureDataExplorerExporterOptionsHelper.RegisterNamedOptions(services, name, configure));

        return builder.AddReader(sp =>
        {
            var options = AzureDataExplorerExporterOptionsHelper.GetExporterOptions(sp, name, configure);
            var readerOptions = AzureDataExplorerExporterOptionsHelper.GetOptions<MetricReaderOptions>(sp, name);
            var periodicOptions = readerOptions.PeriodicExportingMetricReaderOptions;

#pragma warning disable CA2000 // Dispose objects before losing scope
            var exporter = new AzureDataExplorerMetricExporter(options);
#pragma warning restore CA2000 // Dispose objects before losing scope

            return new PeriodicExportingMetricReader(
                exporter,
                periodicOptions.ExportIntervalMilliseconds ?? 60000,
                periodicOptions.ExportTimeoutMilliseconds ?? 30000)
            {
                TemporalityPreference = readerOptions.TemporalityPreference,
            };
        });
    }
}
