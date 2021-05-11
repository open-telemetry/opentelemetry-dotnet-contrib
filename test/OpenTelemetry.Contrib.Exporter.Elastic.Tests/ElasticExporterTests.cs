// <copyright file="ElasticExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Tests
{
    public class ElasticExporterTests
    {
        static ElasticExporterTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void GivenNullBuilder_WhenUseExporter_Throws()
        {
            TracerProviderBuilder builder = null;

            Assert.Throws<ArgumentNullException>(() => builder.UseElasticExporter());
        }

        [Fact]
        public void GivenExporter_WhenProcessInternalActivity_ExportSpan()
        {
            var messageHandler = new TestHttpMessageHandler();
            var httpClient = new HttpClient(messageHandler) { BaseAddress = new Uri("http://localhost/") };
            var options = new ElasticOptions();
            var exporter = new ElasticExporter(options, httpClient);
            var processor = new BatchActivityExportProcessor(exporter);

            var source = new ActivitySource("elastic-test");
            using Activity activity = source.StartActivity("Test Activity");
            processor.OnEnd(activity);
            processor.Shutdown();

            Assert.Contains("\"span\":{\"name\":\"Test Activity\"", messageHandler.Content);
        }

        [Theory]
        [InlineData(ActivityKind.Client)]
        [InlineData(ActivityKind.Consumer)]
        [InlineData(ActivityKind.Producer)]
        [InlineData(ActivityKind.Server)]
        public void GivenExporter_WhenProcessActivity_ExportTransaction(ActivityKind activityKind)
        {
            var messageHandler = new TestHttpMessageHandler();
            var httpClient = new HttpClient(messageHandler) { BaseAddress = new Uri("http://localhost/") };
            var options = new ElasticOptions();
            var exporter = new ElasticExporter(options, httpClient);
            var processor = new BatchActivityExportProcessor(exporter);

            var source = new ActivitySource("elastic-test");
            using Activity activity = source.StartActivity("Test Activity", activityKind);
            processor.OnEnd(activity);
            processor.Shutdown();

            Assert.Contains("\"transaction\":{\"name\":\"Test Activity\"", messageHandler.Content);
        }

        [Fact]
        public void GivenExporter_WhenProcessActivity_ExportResultSuccess()
        {
            ExportResult result = ExportResult.Failure;
            var messageHandler = new TestHttpMessageHandler();
            var httpClient = new HttpClient(messageHandler) { BaseAddress = new Uri("http://localhost/") };
            var options = new ElasticOptions();
            var exporter = new ElasticExporter(options, httpClient);
            var passthroughExporter = new TestExporter<Activity>(Export);
            var processor = new BatchActivityExportProcessor(passthroughExporter);

            var source = new ActivitySource("elastic-test");
            using Activity activity = source.StartActivity("Test Activity");
            processor.OnEnd(activity);
            processor.Shutdown();

            void Export(Batch<Activity> batch)
            {
                result = exporter.Export(batch);
            }

            Assert.StrictEqual(ExportResult.Success, result);
        }

        [Fact]
        public void GivenExporter_WhenProcessBadActivity_ExportResultFailure()
        {
            ExportResult result = ExportResult.Success;
            var messageHandler = new TestHttpMessageHandler(HttpStatusCode.BadRequest);
            var httpClient = new HttpClient(messageHandler) { BaseAddress = new Uri("http://localhost/") };
            var options = new ElasticOptions();
            var exporter = new ElasticExporter(options, httpClient);
            var passthroughExporter = new TestExporter<Activity>(Export);
            var processor = new BatchActivityExportProcessor(passthroughExporter);

            var source = new ActivitySource("elastic-test");
            using Activity activity = source.StartActivity("Test Activity");
            processor.OnEnd(activity);
            processor.Shutdown();

            void Export(Batch<Activity> batch)
            {
                result = exporter.Export(batch);
            }

            Assert.StrictEqual(ExportResult.Failure, result);
        }

        private class TestHttpMessageHandler : DelegatingHandler
        {
            private readonly HttpStatusCode responseStatusCode;

            public TestHttpMessageHandler(HttpStatusCode responseStatusCode = HttpStatusCode.Accepted)
            {
                this.responseStatusCode = responseStatusCode;
            }

            public string Content { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                this.Content = await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(this.responseStatusCode);
            }
        }
    }
}
