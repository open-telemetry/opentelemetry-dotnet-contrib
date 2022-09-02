// <copyright file="HttpSemanticConventionsTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;
using Moq;
using OpenTelemetry.Instrumentation.AWSLambda.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AWSLambda.Tests.Implementation
{
    public class HttpSemanticConventionsTests
    {
        private static readonly ActivitySource TestActivitySource = new("TestActivitySource");

        [Fact]
        public void GetHttpTags_APIGatewayProxyRequest_ReturnsCorrectTags()
        {
            var request = new APIGatewayProxyRequest
            {
                MultiValueHeaders = new Dictionary<string, IList<string>>
                {
                    { "x-forwarded-proto", new List<string> { "https" } },
                    { "x-forwarded-port", new List<string> { "8080" } },
                    { "host", new List<string> { "localhost" } },
                },
                Path = "/path/test",
                HttpMethod = "GET",
            };

            var actualTags = HttpSemanticConventions.GetHttpTags(request);

            var expectedTags = new Dictionary<string, object>
            {
                { "http.scheme", "https" },
                { "http.target", "/path/test" },
                { "net.host.name", "localhost" },
                { "net.host.port", "8080" },
                { "http.method", "GET" },
            };

            AssertTags(expectedTags, actualTags);
        }

        [Fact]
        public void GetHttpTags_APIGatewayHttpApiV2ProxyRequest_ReturnsCorrectTags()
        {
            var request = new APIGatewayHttpApiV2ProxyRequest
            {
                Headers = new Dictionary<string, string>
                {
                    { "x-forwarded-proto",  "https" },
                    { "x-forwarded-port", "8080" },
                    { "host", "localhost" },
                },
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Path = "/path/test",
                        Method = "GET",
                    },
                },
            };

            var actualTags = HttpSemanticConventions.GetHttpTags(request);

            var expectedTags = new Dictionary<string, object>
            {
                { "http.scheme", "https" },
                { "http.target", "/path/test" },
                { "net.host.name", "localhost" },
                { "net.host.port", "8080" },
                { "http.method", "GET" },
            };

            AssertTags(expectedTags, actualTags);
        }

        [Fact]
        public void SetHttpTagsFromResult_APIGatewayProxyResponse_SetsCorrectTags()
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = 200,
            };
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddSource("TestActivitySource")
                .Build();

            using var activity = TestActivitySource.StartActivity("TestActivity");

            HttpSemanticConventions.SetHttpTagsFromResult(activity, response);

            var expectedTags = new Dictionary<string, string>
            {
                { "http.status_code", "200" },
            };
            AssertTags(expectedTags, activity.Tags);
        }

        private static void AssertTags<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> expectedTags, IEnumerable<KeyValuePair<TKey, TValue>> actualTags)
        {
            Assert.NotNull(actualTags);
            Assert.Equal(expectedTags.Count, actualTags.Count());
            foreach (var tag in expectedTags)
            {
                Assert.Contains(new KeyValuePair<TKey, TValue>(tag.Key, tag.Value), actualTags);
            }
        }
    }
}
