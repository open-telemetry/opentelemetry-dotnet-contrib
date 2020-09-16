// <copyright file="ElasticsearchClientTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Moq;
using Nest;
using OpenTelemetry.Trace;
using Xunit;
using Status = OpenTelemetry.Trace.Status;

namespace OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient.Tests
{
    public class ElasticsearchClientTests
    {
        public ElasticsearchClientTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        }

        [Fact]
        public async Task CanCaptureSearchCall()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").DisableDirectStreaming());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>();
                Assert.NotNull(searchResponse);
                Assert.True(searchResponse.ApiCall.Success);
                Assert.NotEmpty(searchResponse.ApiCall.AuditTrail);

                var failed = searchResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
                Assert.Empty(failed);
            }

            // OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(4, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST customer", searchActivity.DisplayName);

            Assert.Equal("localhost", searchActivity.GetTagValue(Constants.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(Constants.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(Constants.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(Constants.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(Constants.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("# Request:", debugInfo);

            Assert.Equal(Status.Ok, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }
    }
}
