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
        [Fact]
        public void GetHttpTags_APIGatewayProxyRequest_ReturnsCorrectTags()
        {
            var request = new APIGatewayProxyRequest
            {
                MultiValueHeaders = new Dictionary<string, IList<string>>
                {
                    { "X-Forwarded-Proto", new List<string> { "https" } },
                    { "Host", new List<string> { "localhost:1234" } },
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
                { "net.host.port", 1234 },
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
                    { "X-Forwarded-Proto",  "https" },
                    { "Host", "localhost:1234" },
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
                { "net.host.port", 1234 },
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

            using var testActivitySource = new ActivitySource("TestActivitySource");
            using var activity = testActivitySource.StartActivity("TestActivity");

            HttpSemanticConventions.SetHttpTagsFromResult(activity, response);

            var expectedTags = new Dictionary<string, string>
            {
                { "http.status_code", "200" },
            };
            AssertTags(expectedTags, activity.Tags);
        }

        [Fact]
        public void SetHttpTagsFromResult_APIGatewayHttpApiV2ProxyResponse_SetsCorrectTags()
        {
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
            };
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddSource("TestActivitySource")
                .Build();

            using var testActivitySource = new ActivitySource("TestActivitySource");
            using var activity = testActivitySource.StartActivity("TestActivity");

            HttpSemanticConventions.SetHttpTagsFromResult(activity, response);

            var expectedTags = new Dictionary<string, string>
            {
                { "http.status_code", "200" },
            };
            AssertTags(expectedTags, activity.Tags);
        }

        [Theory]
        [InlineData(null, null, null, null)]
        [InlineData("", "", "", null)]
        [InlineData(null, "localhost:4321", "localhost", 4321)]
        [InlineData(null, "localhost:4321, myhost.com:9876", "localhost", 4321)]
        [InlineData(null, "localhost", "localhost", null)]
        [InlineData("http", "localhost", "localhost", 80)]
        [InlineData("https", "localhost", "localhost", 443)]
        public void GetHostAndPort_HostHeader_ReturnsCorrectHostAndPort(string httpSchema, string hostHeader, string expectedHost, int? expectedPort)
        {
            (var host, var port) = HttpSemanticConventions.GetHostAndPort(httpSchema, hostHeader);

            Assert.Equal(expectedHost, host);
            Assert.Equal(expectedPort, port);
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
