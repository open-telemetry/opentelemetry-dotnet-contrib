namespace OpenTelemetry.Contrib.Exporter.ElasticApm
{
    /// <summary>
    /// Elastic APM Server events intake API versions.
    /// </summary>
    public sealed class IntakeApiVersion
    {
        /// <summary>
        /// Intake API v2.
        /// </summary>
        public static readonly IntakeApiVersion V2 = new IntakeApiVersion("/intake/v2/events");

        private readonly string endpoint;

        private IntakeApiVersion(string endpoint)
        {
            this.endpoint = endpoint;
        }

        /// <summary>
        /// Implicit operator to string for endpoint field.
        /// </summary>
        /// <param name="value">The intake API version.</param>
        public static implicit operator string(IntakeApiVersion value) => value.endpoint;
    }
}
