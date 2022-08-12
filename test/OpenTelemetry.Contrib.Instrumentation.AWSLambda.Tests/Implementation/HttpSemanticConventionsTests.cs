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
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Tests.Implementation
{
    public class HttpSemanticConventionsTests
    {
        [Fact]
        public void GetHttpTags_APIGatewayProxyRequest_CorrectTags()
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

            var tags = HttpSemanticConventions.GetHttpTags(request);

            Assert.NotNull(tags);
            Assert.Equal(5, tags.Count());
            Assert.Contains(new KeyValuePair<string, object>("http.scheme", "https"), tags);
            Assert.Contains(new KeyValuePair<string, object>("http.target", "/path/test"), tags);
            Assert.Contains(new KeyValuePair<string, object>("net.host.name", "localhost"), tags);
            Assert.Contains(new KeyValuePair<string, object>("net.host.port", "8080"), tags);
            Assert.Contains(new KeyValuePair<string, object>("http.method", "GET"), tags);
        }
    }
}
