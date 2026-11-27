// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.AzureDataExplorer;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering the Azure Data Explorer trace exporter.
/// </summary>
public static class AzureDataExplorerTraceExporterExtensions
{
    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAzureDataExplorerExporter(this TracerProviderBuilder builder)
        => AddAzureDataExplorerExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="AzureDataExplorerExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAzureDataExplorerExporter(this TracerProviderBuilder builder, Action<AzureDataExplorerExporterOptions> configure)
        => AddAzureDataExplorerExporter(builder, name: null, configure);

    /// <summary>
    /// Adds an Azure Data Explorer exporter to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="AzureDataExplorerExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAzureDataExplorerExporter(
        this TracerProviderBuilder builder,
        string? name,
        Action<AzureDataExplorerExporterOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        builder.ConfigureServices(services =>
            AzureDataExplorerExporterOptionsHelper.RegisterNamedOptions(services, name, configure));

        return builder.AddProcessor(sp =>
        {
            var options = AzureDataExplorerExporterOptionsHelper.GetExporterOptions(sp, name, configure);
            var batchOptions = AzureDataExplorerExporterOptionsHelper.GetOptions<BatchExportActivityProcessorOptions>(sp, name);

#pragma warning disable CA2000 // Dispose objects before losing scope
            var exporter = new AzureDataExplorerTraceExporter(options);
#pragma warning restore CA2000 // Dispose objects before losing scope

            return new BatchActivityExportProcessor(
                exporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        });
    }
}
