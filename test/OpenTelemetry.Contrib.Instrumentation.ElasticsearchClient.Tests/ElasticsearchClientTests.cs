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
using System.Text;
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
        public async Task CanCaptureGetById()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var getResponse = await client.GetAsync<Customer>("123");
                Assert.NotNull(getResponse);
                Assert.True(getResponse.ApiCall.Success);
                Assert.NotEmpty(getResponse.ApiCall.AuditTrail);

                var failed = getResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
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

            Assert.Equal($"Elasticsearch GET customer", searchActivity.DisplayName);

            Assert.Equal("localhost", searchActivity.GetTagValue(Constants.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(Constants.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(Constants.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(Constants.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(Constants.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureGetByIdNotFound()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection(null, statusCode: 404)).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var getResponse = await client.GetAsync<Customer>("123");
                Assert.NotNull(getResponse);
                Assert.True(getResponse.ApiCall.Success);
                Assert.NotEmpty(getResponse.ApiCall.AuditTrail);

                var failed = getResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
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

            Assert.Equal($"Elasticsearch GET customer", searchActivity.DisplayName);

            Assert.Equal("localhost", searchActivity.GetTagValue(Constants.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(Constants.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(Constants.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(Constants.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(Constants.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (404) low level call", debugInfo);

            Assert.Equal(Status.Error, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCall()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
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
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCallWithDebugMode()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
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
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCallWithParseAndFormatRequestOption()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation(o => o.ParseAndFormatRequest = true)
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
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
            Assert.Equal(
                @"POST http://localhost:9200/customer/_search?pretty=true&error_trace=true&typed_keys=true
{
  ""query"": {
    ""bool"": {
      ""must"": [
        {
          ""term"": {
            ""id"": {
              ""value"": ""123""
            }
          }
        }
      ]
    }
  }
}".Replace("\r\n", "\n"),
                debugInfo.Replace("\r\n", "\n"));

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCallWithoutDebugMode()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation(o => o.ParseAndFormatRequest = true)
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
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
            Assert.DoesNotContain("123", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureMultipleIndiceSearchCall()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer,order"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
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

            Assert.Equal($"Elasticsearch POST", searchActivity.DisplayName);

            Assert.Equal("localhost", searchActivity.GetTagValue(Constants.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(Constants.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(Constants.AttributeDbSystem));
            Assert.Null(searchActivity.GetTagValue(Constants.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(Constants.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureElasticsearchClientException()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var connection = new InMemoryConnection(Encoding.UTF8.GetBytes("{}"), statusCode: 500, exception: new ElasticsearchClientException("Boom"));
            var client = new ElasticClient(new ConnectionSettings(connection).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
                Assert.NotNull(searchResponse);
                Assert.False(searchResponse.ApiCall.Success);
                Assert.NotEmpty(searchResponse.ApiCall.AuditTrail);

                var failed = searchResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
                Assert.NotEmpty(failed);
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
            Assert.Contains("Unsuccessful (500) low level call", debugInfo);

            var status = searchActivity.GetStatus();
            Assert.Equal(Status.Error.StatusCode, status.StatusCode);
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureCatRequest()
        {
            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResource(expectedResource)
                .AddProcessor(processor.Object)
                .Build())
            {
                var getResponse = await client.Cat.IndicesAsync();
                Assert.NotNull(getResponse);
                Assert.True(getResponse.ApiCall.Success);
                Assert.NotEmpty(getResponse.ApiCall.AuditTrail);

                var failed = getResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
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

            Assert.Equal($"Elasticsearch GET", searchActivity.DisplayName);

            Assert.Equal("localhost", searchActivity.GetTagValue(Constants.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(Constants.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(Constants.AttributeDbSystem));
            Assert.Null(searchActivity.GetTagValue(Constants.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(Constants.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());
            Assert.Equal(expectedResource, searchActivity.GetResource());
        }
    }
}
