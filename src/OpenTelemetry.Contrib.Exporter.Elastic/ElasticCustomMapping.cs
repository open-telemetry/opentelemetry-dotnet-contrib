using System;
using System.Net;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    /// <summary>
    /// Custom mappings for transactions and spans.
    /// </summary>
    public class ElasticCustomMapping
    {
        /// <summary>
        /// Gets or sets custom mapping for Elastic APM transaction result. Default OTel StatusCode with Ok for Http Success status code.
        /// https://github.com/elastic/apm-server/blob/32f34ed4298d648bf9476790f2a8a54d72805bb6/docs/spec/v2/transaction.json#L680.
        /// </summary>
        public Func<HttpStatusCode?, StatusCode?, string> TransactionResult { get; set; } =
            (httpStatusCode, otelStatusCode) => otelStatusCode switch
            {
                StatusCode.Ok => "Ok",
                StatusCode.Error => "Error",
                StatusCode.Unset => httpStatusCode.HasValue
                    ? ((int)httpStatusCode >= 200) && ((int)httpStatusCode <= 299)
                        ? "Ok"
                        : "Error"
                    : "Unset",
                _ => "Unknown"
            };
    }
}
