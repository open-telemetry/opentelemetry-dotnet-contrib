using System;
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Geneva
{
    public static class GenevaExporterHelperExtensions
    {
        public static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, Action<GenevaExporterOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    AddGenevaTraceExporter(builder, sp.GetOptions<GenevaExporterOptions>(), configure);
                });
            }

            return AddGenevaTraceExporter(builder, new GenevaExporterOptions(), configure);
        }

        private static TracerProviderBuilder AddGenevaTraceExporter(this TracerProviderBuilder builder, GenevaExporterOptions options, Action<GenevaExporterOptions> configure)
        {
            configure?.Invoke(options);
            var exporter = new GenevaTraceExporter(options);
            if (exporter.IsUsingUnixDomainSocket)
            {
                return builder.AddProcessor(new BatchActivityExportProcessor(exporter));
            }
            else
            {
                return builder.AddProcessor(new ReentrantExportProcessor<Activity>(exporter));
            }
        }
    }
}
