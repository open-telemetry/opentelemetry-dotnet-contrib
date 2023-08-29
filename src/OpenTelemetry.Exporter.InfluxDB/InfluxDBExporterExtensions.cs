// <copyright file="InfluxDBExporterExtensions.cs" company="OpenTelemetry Authors">
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
using InfluxDB.Client;
using OpenTelemetry.Exporter.InfluxDB;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> for using InfluxDB.
/// </summary>
public static class InfluxDBExporterExtensions
{
    /// <summary>
    /// Adds InfluxDB exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="InfluxDBMetricsExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInfluxDBMetricsExporter(this MeterProviderBuilder builder, Action<InfluxDBMetricsExporterOptions> configure)
    {
        builder.AddReader(_ =>
        {
            var options = new InfluxDBMetricsExporterOptions();
            configure.Invoke(options);

            var influxDbClientOptions = new InfluxDBClientOptions(options.Endpoint?.ToString());
            if (!string.IsNullOrWhiteSpace(options.Bucket))
            {
                influxDbClientOptions.Bucket = options.Bucket;
            }

            if (!string.IsNullOrWhiteSpace(options.Org))
            {
                influxDbClientOptions.Org = options.Org;
            }

            if (!string.IsNullOrWhiteSpace(options.Token))
            {
                influxDbClientOptions.Token = options.Token;
            }

            var influxDbClient = new InfluxDBClient(influxDbClientOptions);
            var writeApi = influxDbClient.GetWriteApi(new WriteOptions
            {
                FlushInterval = options.FlushInterval,
            });
            var metricsWriter = CreateMetricsWriter(options.MetricsSchema);
            var exporter = new InfluxDBMetricsExporter(metricsWriter, influxDbClient, writeApi);
            return new PeriodicExportingMetricReader(exporter)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
            };
        });
        return builder;
    }

    private static IMetricsWriter CreateMetricsWriter(MetricsSchema metricsSchema)
    {
        return metricsSchema switch
        {
            MetricsSchema.TelegrafPrometheusV2 => new TelegrafPrometheusWriterV2(),
            _ => new TelegrafPrometheusWriterV1(),
        };
    }
}
