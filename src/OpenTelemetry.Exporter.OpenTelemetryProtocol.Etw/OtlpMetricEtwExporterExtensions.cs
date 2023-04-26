// <copyright file="OtlpMetricEtwExporterExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Etw;
public static class OtlpMetricEtwExporterExtensions
{
    public static MeterProviderBuilder AddOtlpEtwExporter(
        this MeterProviderBuilder builder,
        Action<OtlpEtwExporterOptions, MetricReaderOptions> configureExporterAndMetricReader = null)
    {
        Guard.ThrowIfNull(builder);

        if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            return deferredMeterProviderBuilder.Configure((sp, builder) =>
            {
                var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpEtwExporterOptions>>().Get(null);
                var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(null);

                AddOtlpEtwExporter(builder, exporterOptions, metricReaderOptions, configureExporterAndMetricReader);
            });
        }

        return AddOtlpEtwExporter(builder, new OtlpEtwExporterOptions(), new MetricReaderOptions(), configureExporterAndMetricReader);
    }

    private static MeterProviderBuilder AddOtlpEtwExporter(
        MeterProviderBuilder builder,
        OtlpEtwExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions,
        Action<OtlpEtwExporterOptions, MetricReaderOptions> configure = null)
    {
        configure?.Invoke(exporterOptions, metricReaderOptions);

        const int defaultExportIntervalMilliseconds = 60000;
        const int defaultExportTimeoutMilliseconds = 30000;
        var exportInterval = metricReaderOptions.PeriodicExportingMetricReaderOptions?.ExportIntervalMilliseconds ?? defaultExportIntervalMilliseconds;
        var exportTimeout = metricReaderOptions.PeriodicExportingMetricReaderOptions?.ExportTimeoutMilliseconds ?? defaultExportTimeoutMilliseconds;

        return builder.AddReader(new PeriodicExportingMetricReader(new OtlpEtwMetricExporter(exporterOptions), exportInterval, exportTimeout)
        {
            TemporalityPreference = metricReaderOptions.TemporalityPreference,
        });
    }
}
