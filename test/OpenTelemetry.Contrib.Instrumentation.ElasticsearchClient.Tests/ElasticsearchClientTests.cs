﻿// <copyright file="ElasticsearchClientTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Moq;
using Nest;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
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
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch GET customer", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureGetByIdNotFound()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection(null, statusCode: 404)).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch GET customer", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (404) low level call", debugInfo);

            Assert.Equal(Status.Error, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCall()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST customer", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanRecordAndSampleSearchCall()
        {
            bool samplerCalled = false;

            var sampler = new TestSampler
            {
                SamplingAction =
                (samplingParameters) =>
                {
                    samplerCalled = true;
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            int startCalled = 0;
            int endCalled = 0;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.True(samplerCalled);
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled++;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled++;
                };

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnectionWithDownstreamActivity()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(sampler)
                .AddSource("Downstream")
                .AddSource("NestedDownstream")
                .AddElasticsearchClientInstrumentation((opt) => opt.SuppressDownstreamInstrumentation = false)
                .AddProcessor(testActivityProcessor)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
                Assert.NotNull(searchResponse);
                Assert.True(searchResponse.ApiCall.Success);
                Assert.NotEmpty(searchResponse.ApiCall.AuditTrail);

                var failed = searchResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
                Assert.Empty(failed);
            }

            Assert.Equal(3, startCalled); // Processor.OnStart is called since we added a legacy OperationName
            Assert.Equal(3, endCalled); // Processor.OnEnd is called since we added a legacy OperationName
        }

        [Fact]
        public async Task CanSupressDownstreamActivities()
        {
            bool samplerCalled = false;

            var sampler = new TestSampler
            {
                SamplingAction =
                (samplingParameters) =>
                {
                    samplerCalled = true;
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            int startCalled = 0;
            int endCalled = 0;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.True(samplerCalled);
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled++;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled++;
                };

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnectionWithDownstreamActivity()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(sampler)
                .AddSource("Downstream")
                .AddSource("NestedDownstream")
                .AddElasticsearchClientInstrumentation((opt) => opt.SuppressDownstreamInstrumentation = true)
                .AddProcessor(testActivityProcessor)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
                Assert.NotNull(searchResponse);
                Assert.True(searchResponse.ApiCall.Success);
                Assert.NotEmpty(searchResponse.ApiCall.AuditTrail);

                var failed = searchResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
                Assert.Empty(failed);
            }

            Assert.Equal(1, startCalled); // Processor.OnStart is called since we added a legacy OperationName
            Assert.Equal(1, endCalled); // Processor.OnEnd is called since we added a legacy OperationName
        }

        [Fact]
        public async Task CanDropSearchCall()
        {
            bool samplerCalled = false;

            var sampler = new TestSampler
            {
                SamplingAction =
                (samplingParameters) =>
                {
                    samplerCalled = true;
                    return new SamplingResult(SamplingDecision.Drop);
                },
            };

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            int startCalled = 0;
            int endCalled = 0;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.True(samplerCalled);
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.False(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled++;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.False(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled++;
                };

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnectionWithDownstreamActivity()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(sampler)
                .AddSource("Downstream")
                .AddSource("NestedDownstream")
                .AddElasticsearchClientInstrumentation((opt) => opt.SuppressDownstreamInstrumentation = false)
                .AddProcessor(testActivityProcessor)
                .Build())
            {
                var searchResponse = await client.SearchAsync<Customer>(s => s.Query(q => q.Bool(b => b.Must(m => m.Term(f => f.Id, "123")))));
                Assert.NotNull(searchResponse);
                Assert.True(searchResponse.ApiCall.Success);
                Assert.NotEmpty(searchResponse.ApiCall.AuditTrail);

                var failed = searchResponse.ApiCall.AuditTrail.Where(a => a.Event == AuditEvent.BadResponse);
                Assert.Empty(failed);
            }

            Assert.Equal(0, startCalled); // Processor.OnStart is called since we added a legacy OperationName
            Assert.Equal(0, endCalled); // Processor.OnEnd is called since we added a legacy OperationName
        }

        [Fact]
        public async Task CanCaptureSearchCallWithDebugMode()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST customer", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCallWithParseAndFormatRequestOption()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation(o => o.ParseAndFormatRequest = true)
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST customer", searchActivity.DisplayName);

            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
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

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureSearchCallWithoutDebugMode()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation(o => o.ParseAndFormatRequest = true)
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST customer", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.DoesNotContain("123", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureMultipleIndiceSearchCall()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer,order"));

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Null(searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureElasticsearchClientException()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var connection = new InMemoryConnection(Encoding.UTF8.GetBytes("{}"), statusCode: 500, exception: new ElasticsearchClientException("Boom"));
            var client = new ElasticClient(new ConnectionSettings(connection).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch POST customer", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("customer", searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Unsuccessful (500) low level call", debugInfo);

            var status = searchActivity.GetStatus();
            Assert.Equal(Status.Error.StatusCode, status.StatusCode);

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }

        [Fact]
        public async Task CanCaptureCatRequest()
        {
            var expectedResource = ResourceBuilder.CreateDefault().AddService("test-service");
            var processor = new Mock<BaseProcessor<Activity>>();

            var parent = new Activity("parent").Start();

            var client = new ElasticClient(new ConnectionSettings(new InMemoryConnection()).DefaultIndex("customer").EnableDebugMode());

            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddElasticsearchClientInstrumentation()
                .SetResourceBuilder(expectedResource)
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

            // SetParentProvider, OnStart, OnEnd, OnShutdown, Dispose
            Assert.Equal(5, processor.Invocations.Count);
            var activities = processor.Invocations.Where(i => i.Method.Name == "OnEnd").Select(i => i.Arguments[0]).Cast<Activity>().ToArray();
            Assert.Single(activities);

            var searchActivity = activities[0];

            Assert.Equal(parent.TraceId, searchActivity.Context.TraceId);
            Assert.Equal(parent.SpanId, searchActivity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, searchActivity.Context.SpanId);
            Assert.NotEqual(default, searchActivity.Context.SpanId);

            Assert.Equal($"Elasticsearch GET", searchActivity.DisplayName);

            var tags = searchActivity.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            Assert.Equal("localhost", searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            Assert.Equal(9200, searchActivity.GetTagValue(SemanticConventions.AttributeNetPeerPort));

            Assert.Equal("elasticsearch", searchActivity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Null(searchActivity.GetTagValue(SemanticConventions.AttributeDbName));
            var debugInfo = (string)searchActivity.GetTagValue(SemanticConventions.AttributeDbStatement);
            Assert.NotEmpty(debugInfo);
            Assert.Contains("Successful (200) low level call", debugInfo);

            Assert.Equal(Status.Unset, searchActivity.GetStatus());

            // Assert.Equal(expectedResource, searchActivity.GetResource());
        }
    }
}
