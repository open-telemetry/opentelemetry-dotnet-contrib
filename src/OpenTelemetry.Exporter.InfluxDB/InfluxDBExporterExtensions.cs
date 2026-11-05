// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using InfluxDB.Client;
using OpenTelemetry.Exporter.InfluxDB;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> for using InfluxDB.
/// </summary>
public static class InfluxDBExporterExtensions
{
    private const int MaxResponseContentBufferSize = 1024 * 1024;

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

            // InfluxDB.Client does not configure or dispose a supplied HttpClient. Match
            // its RestSharp handler settings while bounding buffered write responses.
#pragma warning disable CA5399 // Match RestSharp's existing certificate revocation behavior on all target frameworks.
            var httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = influxDbClientOptions.AllowHttpRedirects,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.None,
            };
#pragma warning restore CA5399

            if (!influxDbClientOptions.VerifySsl)
            {
#pragma warning disable CA5359 // Preserve an explicitly disabled InfluxDB endpoint certificate check.
#if NET
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#else
                httpClientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif
#pragma warning restore CA5359
            }

#pragma warning disable CA5400 // Match RestSharp's existing certificate revocation behavior on all target frameworks.
            var httpClient = new HttpClient(httpClientHandler)
            {
                MaxResponseContentBufferSize = MaxResponseContentBufferSize,
                Timeout = Timeout.InfiniteTimeSpan,
            };
#pragma warning restore CA5400

            InfluxDBClient? influxDbClient = null;
            try
            {
                influxDbClientOptions.HttpClient = httpClient;
                influxDbClient = new InfluxDBClient(influxDbClientOptions);
                var metricsWriter = CreateMetricsWriter(options.MetricsSchema);
                var exporter = new InfluxDBMetricsExporter(metricsWriter, influxDbClient, httpClient);
                return new PeriodicExportingMetricReader(exporter, options.MetricExportIntervalMilliseconds)
                {
                    TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
                };
            }
            catch
            {
                influxDbClient?.Dispose();
                httpClient.Dispose();
                throw;
            }
        });
        return builder;
    }

    private static IMetricsWriter CreateMetricsWriter(MetricsSchema metricsSchema) => metricsSchema switch
    {
        MetricsSchema.TelegrafPrometheusV2 => new TelegrafPrometheusWriterV2(),
        MetricsSchema.TelegrafPrometheusV1 => new TelegrafPrometheusWriterV1(),
        MetricsSchema.None => new TelegrafPrometheusWriterV1(),
        _ => new TelegrafPrometheusWriterV1(),
    };
}
