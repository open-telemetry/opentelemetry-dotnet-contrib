// <copyright file="ElasticExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
