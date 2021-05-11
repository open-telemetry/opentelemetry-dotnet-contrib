// <copyright file="NdjsonContent.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenTelemetry.Contrib.Exporter.Elastic.Implementation;

namespace OpenTelemetry.Contrib.Exporter.Elastic
{
    internal class NdjsonContent : HttpContent
    {
        private static readonly MediaTypeHeaderValue NdjsonHeader =
            new MediaTypeHeaderValue("application/x-ndjson")
            {
                CharSet = new UTF8Encoding(false).WebName,
            };

        private readonly ElasticOptions options;
        private readonly Batch<Activity> batch;
        private readonly IJsonSerializable metadata;
        private Utf8JsonWriter writer;

        public NdjsonContent(ElasticOptions options, in Batch<Activity> batch)
        {
            this.options = options;
            this.batch = batch;
            this.metadata = this.options.CreateMetadata();

            this.Headers.ContentType = NdjsonHeader;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            this.EnsureWriter(stream);

            this.metadata.Write(this.writer);
            this.writer.WriteNewLine(stream);

            foreach (var activity in this.batch)
            {
                var span = activity.ToElasticApmSpan(this.options);
                span.Write(this.writer);
                this.writer.WriteNewLine(stream);
            }

            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        private void EnsureWriter(Stream stream)
        {
            if (this.writer == null)
            {
                this.writer = new Utf8JsonWriter(stream);
            }
            else
            {
                this.writer.Reset(stream);
            }
        }
    }
}
