// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.AzureDataExplorer;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Extension methods to simplify registering the Azure Data Explorer log exporter.
/// </summary>
public static class AzureDataExplorerLogExporterExtensions
{
    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="LoggerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddAzureDataExplorerExporter(this LoggerProviderBuilder builder)
        => AddAzureDataExplorerExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="LoggerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="AzureDataExplorerExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddAzureDataExplorerExporter(this LoggerProviderBuilder builder, Action<AzureDataExplorerExporterOptions> configure)
        => AddAzureDataExplorerExporter(builder, name: null, configure);

    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="LoggerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="AzureDataExplorerExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddAzureDataExplorerExporter(
        this LoggerProviderBuilder builder,
        string? name,
        Action<AzureDataExplorerExporterOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        builder.ConfigureServices(services =>
            AzureDataExplorerExporterOptionsHelper.RegisterNamedOptions(services, name, configure));

        return builder.AddProcessor(sp =>
        {
            var options = AzureDataExplorerExporterOptionsHelper.GetExporterOptions(sp, name, configure);
            var batchOptions = AzureDataExplorerExporterOptionsHelper.GetOptions<BatchExportLogRecordProcessorOptions>(sp, name);

#pragma warning disable CA2000 // Dispose objects before losing scope
            var exporter = new AzureDataExplorerLogExporter(options);
#pragma warning restore CA2000 // Dispose objects before losing scope

            return new BatchLogRecordExportProcessor(
                exporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        });
    }
}
