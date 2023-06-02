// <copyright file="GenevaExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Geneva;

public static class GenevaExporterHelperExtensions
{
    public static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, Action<GenevaExporterOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        return builder.AddProcessor(sp =>
        {
            var exporterOptions = sp.GetOptions<GenevaExporterOptions>();

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
