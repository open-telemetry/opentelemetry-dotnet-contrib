// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Geneva;

public static class GenevaExporterHelperExtensions
{
    /// <summary>
    /// Adds <see cref="GenevaTraceExporter"/> to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Exporter configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, Action<GenevaExporterOptions> configure)
        => AddGenevaTraceExporter(builder, name: null, configure);

    /// <summary>
    /// Adds <see cref="GenevaTraceExporter"/> to the <see cref="TracerProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Exporter configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, string name, Action<GenevaExporterOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        return builder.AddProcessor(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<GenevaExporterOptions>>().Get(name);

            return BuildGenevaTraceExporter(exporterOptions, configure);
        });
    }

    private static BaseProcessor<Activity> BuildGenevaTraceExporter(GenevaExporterOptions options, Action<GenevaExporterOptions> configure)
    {
        configure?.Invoke(options);
        var exporter = new GenevaTraceExporter(options);
        if (exporter.IsUsingUnixDomainSocket)
        {
            var batchOptions = new BatchExportActivityProcessorOptions();
            return new BatchActivityExportProcessor(
                exporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        }
        else
        {
            return new ReentrantActivityExportProcessor(exporter);
        }
    }
}
