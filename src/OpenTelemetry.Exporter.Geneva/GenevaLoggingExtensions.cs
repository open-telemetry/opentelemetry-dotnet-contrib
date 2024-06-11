// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

public static class GenevaLoggingExtensions
{
    public static OpenTelemetryLoggerOptions AddGenevaLogExporter(this OpenTelemetryLoggerOptions options, Action<GenevaExporterOptions> configure)
    {
        Guard.ThrowIfNull(options);

        var genevaOptions = new GenevaExporterOptions();
        configure?.Invoke(genevaOptions);
        var exporter = new GenevaLogExporter(genevaOptions);
        if (exporter.IsUsingUnixDomainSocket)
        {
            return options.AddProcessor(new BatchLogRecordExportProcessor(exporter));
        }
        else
        {
            return options.AddProcessor(new ReentrantExportProcessor<LogRecord>(exporter));
        }
    }

    /// <summary>
    /// Adds Geneva exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal static LoggerProviderBuilder AddGenevaLogExporter(this LoggerProviderBuilder builder)
        => AddGenevaLogExporter(builder, name: null, configureExporter: null);

    /// <summary>
    /// Adds Geneva exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="GenevaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal static LoggerProviderBuilder AddGenevaLogExporter(this LoggerProviderBuilder builder, Action<GenevaExporterOptions> configureExporter)
        => AddGenevaLogExporter(builder, name: null, configureExporter);

    /// <summary>
    /// Adds Geneva exporter to the LoggerProvider.
    /// </summary>
    /// <param name="builder"><see cref="LoggerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporter">Optional callback action for configuring <see cref="GenevaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal static LoggerProviderBuilder AddGenevaLogExporter(
        this LoggerProviderBuilder builder,
        string name,
        Action<GenevaExporterOptions> configureExporter)
    {
        var finalOptionsName = name ?? Options.Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (name != null && configureExporter != null)
            {
                // If we are using named options we register the
                // configuration delegate into options pipeline.
                services.Configure(finalOptionsName, configureExporter);
            }
        });

        return builder.AddProcessor(sp =>
        {
            GenevaExporterOptions exporterOptions;

            BatchExportLogRecordProcessorOptions batchExportLogRecordProcessorOptions;

            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // OtlpExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together. See:
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/4043
                exporterOptions = sp.GetRequiredService<IOptionsFactory<GenevaExporterOptions>>().Create(finalOptionsName);

                batchExportLogRecordProcessorOptions = sp.GetRequiredService<IOptionsFactory<BatchExportLogRecordProcessorOptions>>().Create(finalOptionsName);

                // Configuration delegate is executed inline on the fresh instance.
                configureExporter?.Invoke(exporterOptions);
            }
            else
            {
                // When using named options we can properly utilize Options
                // API to create or reuse an instance.
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<GenevaExporterOptions>>().Get(finalOptionsName);

                batchExportLogRecordProcessorOptions = sp.GetRequiredService<IOptionsMonitor<BatchExportLogRecordProcessorOptions>>().Get(finalOptionsName);
            }

            return BuildGenevaLogExporter(
                sp,
                batchExportLogRecordProcessorOptions,
                exporterOptions);
        });
    }

    internal static BaseProcessor<LogRecord> BuildGenevaLogExporter(
       IServiceProvider serviceProvider,
       BatchExportLogRecordProcessorOptions batchExportLogRecordProcessorOptions,
       GenevaExporterOptions exporterOptions)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");
        Debug.Assert(exporterOptions != null, "exporterOptions was null");

        var exporter = new GenevaLogExporter(exporterOptions);
        if (exporter.IsUsingUnixDomainSocket)
        {
            return new BatchLogRecordExportProcessor(
                exporter,
                batchExportLogRecordProcessorOptions.MaxQueueSize,
                batchExportLogRecordProcessorOptions.ScheduledDelayMilliseconds,
                batchExportLogRecordProcessorOptions.ExporterTimeoutMilliseconds,
                batchExportLogRecordProcessorOptions.MaxExportBatchSize);
        }
        else
        {
            return new ReentrantExportProcessor<LogRecord>(exporter);
        }
    }
}
