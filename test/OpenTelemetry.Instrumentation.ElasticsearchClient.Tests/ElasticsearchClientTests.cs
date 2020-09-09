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
using Elasticsearch.Net.VirtualizedCluster;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.GrpcClient.Tests
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
            var activityProcessor = new Mock<ActivityProcessor>();

            var parent = new Activity("parent").Start();

            var cluster = VirtualClusterWith.Nodes(1)
                .ClientCalls(c => c.SucceedAlways())
                .StaticConnectionPool()
                .AllDefaults();
            var client = cluster.Client;

            using (Sdk.CreateTracerProviderBuilder()
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(activityProcessor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<StringResponse>(PostData.Empty);
                Assert.NotNull(searchResponse);
                Assert.True(searchResponse.Success);
                Assert.NotEmpty(searchResponse.AuditTrail);

                var failed = searchResponse.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
                Assert.Empty(failed);
            }

            var activities = activityProcessor.Invocations.Select(i => i.Arguments[0]).OfType<Activity>().ToArray();

            Assert.NotEmpty(activities);

            var pings = activities.Where(a => a.OperationName == "Ping");
            Assert.Single(pings);
            Assert.Single(activities.Where(s => s.OperationName == "CallElasticsearch"));

            // spans.Should().OnlyContain(s => s.Context != null);
            // spans.Should().OnlyContain(s => s.Context.Db != null);
            // spans.Should().OnlyContain(s => s.Context.Db.Statement != null);

            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch).Context.Destination.Should().NotBeNull();
            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch).Context.Destination.Address.Should().Be("localhost");
            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch).Context.Destination.Port.Should().Be(9200);

            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch).Context.Destination.Service.Should().NotBeNull();
            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch).Context.Destination.Service.Type.Should().Be(ApiConstants.TypeDb);
            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch)
            //    .Context.Destination.Service.Name.Should()
            //    .Be(ApiConstants.SubtypeElasticsearch);
            // spans.First(n => n.Subtype == ApiConstants.SubtypeElasticsearch)
            //    .Context.Destination.Service.Resource.Should()
            //    .Be(ApiConstants.SubtypeElasticsearch);
        }
    }
}
