using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;

namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    internal class ElasticExporter : BaseExporter<Activity>
    {
        private readonly HttpClient httpClient;
        private readonly ElasticOptions options;

        public ElasticExporter(ElasticOptions options, HttpClient httpClient = null)
        {
            this.options = options;
            this.httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(options.ServerUrl) };
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            using var scope = SuppressInstrumentationScope.Begin();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, this.options.IntakeApiVersion)
                {
                    Content = new NdjsonContent(this.options, batch),
                };

                using var response = this.httpClient
                    .SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();

                response.EnsureSuccessStatusCode();

                return ExportResult.Success;
            }
            catch
            {
                return ExportResult.Failure;
            }
        }
    }
}
