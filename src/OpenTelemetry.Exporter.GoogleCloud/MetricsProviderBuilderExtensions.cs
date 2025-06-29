// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.GoogleCloud;

/// <summary>
/// Extension methods to simplify registering a GoogleCloudMonitoring  exporter.
/// </summary>
public static class GoogleCloudMonitoringExporterProviderBuilderExtensions
{
    /// <summary>
    /// Registers a GoogleCloudMonitoring  exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="projectId">Project ID to send telemetry to.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder UseGoogleCloudMonitoringExporter(
        this MeterProviderBuilder builder,
        string projectId)
    {
        Guard.ThrowIfNull(builder);
        builder.AddReader(_ =>
        {
            var exporter = new GoogleCloudMonitoringExporter(projectId);
            return new PeriodicExportingMetricReader(exporter, 10000) // todo: make this configurable
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
            };
        });
        return builder;
    }
}
