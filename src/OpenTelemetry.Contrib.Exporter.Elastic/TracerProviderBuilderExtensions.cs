using System;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    /// <summary>
    /// Extension methods to register Elastic APM exporter.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Registers a Elastic APM exporter that will receive <see cref="System.Diagnostics.Activity"/> instances.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder UseElasticApmExporter(
            this TracerProviderBuilder builder,
            Action<ElasticApmOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new ElasticApmOptions();
            configure?.Invoke(options);

            var activityExporter = new ElasticExporter(options);

            return builder.AddProcessor(new BatchActivityExportProcessor(activityExporter));
        }
    }
}
