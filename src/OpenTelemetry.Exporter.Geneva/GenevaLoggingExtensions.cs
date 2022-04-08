#if NETSTANDARD2_0 || NET461
using System;
using OpenTelemetry;
using OpenTelemetry.Exporter.Geneva;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging
{
    public static class GenevaLoggingExtensions
    {
        public static OpenTelemetryLoggerOptions AddGenevaLogExporter(this OpenTelemetryLoggerOptions options, Action<GenevaExporterOptions> configure)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

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
    }
}
#endif
