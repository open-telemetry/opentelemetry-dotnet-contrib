namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    /// <summary>
    /// Elastic APM exporter options.
    /// </summary>
    public class ElasticOptions
    {
        /// <summary>
        /// Gets or sets Elastic APM Server host. Default value: http://localhost:8200/.
        /// https://www.elastic.co/guide/en/apm/server/current/configuration-process.html#host.
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:8200/";

        /// <summary>
        /// Gets or sets application environment. Default value: Dev.
        /// </summary>
        public string Environment { get; set; } = "Dev";

        /// <summary>
        /// Gets or sets application name. Default value: MyService.
        /// </summary>
        public string ServiceName { get; set; } = "MyService";

        /// <summary>
        /// Gets or sets Elastic APM Server API version. Default value: IntakeApiVersion.V2.
        /// https://www.elastic.co/guide/en/apm/server/current/events-api.html#events-api-endpoint.
        /// </summary>
        public IntakeApiVersion IntakeApiVersion { get; set; } = IntakeApiVersion.V2;
    }
}
