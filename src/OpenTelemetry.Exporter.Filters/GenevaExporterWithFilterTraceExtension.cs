// <copyright file="GenevaExporterWithFilterTraceExtension.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.Filters;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Geneva;

/// <summary>
///  Extension to add geneva exporter with filter.
/// </summary>
public static class GenevaExporterWithFilterTraceExtension
{
    /// <summary>
    /// Add Geneva Exporter with Filter.
    /// </summary>
    /// <param name="builder">Trace provider builder.</param>
    /// <param name="name">name of the exporter.</param>
    /// <param name="configure">configuration of geneva exporter.</param>
    /// <param name="filter">filter to drop useless activity.</param>
    /// <returns>TracerProviderBuilder.</returns>
    public static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, string name, Action<GenevaExporterOptions> configure, BaseFilter<Activity> filter)
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

            return BuildGenevaTraceExporter(exporterOptions, configure, filter);
        });
    }

    /// <summary>
    /// Add Geneva Exporter with sampler.
    /// </summary>
    /// <param name="builder">Trace provider builder.</param>
    /// <param name="name">name of the exporter.</param>
    /// <param name="configure">configuration of geneva exporter.</param>
    /// <param name="sampler">sampler to drop useless activity.</param>
    /// <returns>TracerProviderBuilder.</returns>
    public static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, string name, Action<GenevaExporterOptions> configure, Sampler sampler)
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

            return BuildGenevaTraceExporter(exporterOptions, configure, new SamplerFilter(sampler));
        });
    }

    private static BaseProcessor<Activity> BuildGenevaTraceExporter(GenevaExporterOptions options, Action<GenevaExporterOptions> configure, BaseFilter<Activity> filter)
    {
        configure?.Invoke(options);
        var exporter = new GenevaTraceExporter(options);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var batchOptions = new BatchExportActivityProcessorOptions();
            return new BatchActivityExportProcessorWithFilter(
                exporter,
                filter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        }
        else
        {
            return new ReentrantActivityExportProcessorWithFilter(exporter, filter);
        }
    }
}
