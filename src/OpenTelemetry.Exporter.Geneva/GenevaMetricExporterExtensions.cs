using System;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Geneva;

public static class GenevaMetricExporterExtensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
    public static MeterProviderBuilder AddGenevaMetricExporter(this MeterProviderBuilder builder, Action<GenevaMetricExporterOptions> configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var options = new GenevaMetricExporterOptions();
        configure?.Invoke(options);
        return builder.AddReader(new PeriodicExportingMetricReader(new GenevaMetricExporter(options), options.MetricExportIntervalMilliseconds));
    }
}
