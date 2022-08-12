// <copyright file="HttpSemanticConventions.cs" company="OpenTelemetry Authors">
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
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation
{
    internal class HttpSemanticConventions
    {
        // x-forwarded-... headres are described here https://docs.aws.amazon.com/elasticloadbalancing/latest/classic/x-forwarded-headers.html
        private const string HeaderXForwardedProto = "x-forwarded-proto";
        private const string HeaderXForwardedPort = "x-forwarded-port";
        private const string HeaderHost = "host";

        internal static IEnumerable<KeyValuePair<string, object>> GetHttpTags<TInput>(TInput input)
        {
            var tags = new List<KeyValuePair<string, object>>();

            string httpScheme = null;
            string httpTarget = null;
            string hostName = null;
            string hostPort = null;
            string httpMethod = null;

            switch (input)
            {
                case APIGatewayProxyRequest request:
                    httpScheme = GetHeaderValue(request, HeaderXForwardedProto);
                    httpTarget = request.Path;
                    hostName = GetHeaderValue(request, HeaderHost);
                    hostPort = GetHeaderValue(request, HeaderXForwardedPort);
                    httpMethod = request.HttpMethod;
                    break;
                case APIGatewayHttpApiV2ProxyRequest requestV2:
                    httpScheme = GetHeaderValue(requestV2, HeaderXForwardedProto);
                    httpTarget = requestV2?.RequestContext.Http?.Path;
                    hostName = GetHeaderValue(requestV2, HeaderHost);
                    hostPort = GetHeaderValue(requestV2, HeaderXForwardedPort);
                    httpMethod = requestV2?.RequestContext?.Http?.Method;
                    break;
            }

            tags.AddStringTagIfNotNull(SemanticConventions.AttributeHttpScheme, httpScheme);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeHttpTarget, httpTarget);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeNetHostName, hostName);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeNetHostPort, hostPort);
            tags.AddStringTagIfNotNull(SemanticConventions.AttributeHttpMethod, httpMethod);

            return tags;
        }

        internal static void SetHttpTagsFromRequest<TResult>(Activity activity, TResult result)
        {
            switch (result)
            {
                case APIGatewayProxyResponse response:
                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);
                    break;
                case APIGatewayHttpApiV2ProxyResponse responseV2:
                    activity.SetTag(SemanticConventions.AttributeHttpStatusCode, responseV2.StatusCode);
                    break;
            }
        }

        private static string GetHeaderValue(APIGatewayProxyRequest request, string name)
        {
            if (request.MultiValueHeaders != null &&
                request.MultiValueHeaders.TryGetValueIgnoringCase(name, out var values))
            {
                return string.Join(",", values);
            }
            else if (request.Headers != null &&
                     request.Headers.TryGetValueIgnoringCase(name, out var value))
            {
                return value;
            }

            return null;
        }

        private static string GetHeaderValue(APIGatewayHttpApiV2ProxyRequest request, string name)
        {
            if (request.Headers != null &&
                request.Headers.TryGetValueIgnoringCase(name, out var header))
            {
                // Multiple values for the same header will be separated by a comma.
                return header;
            }

            return null;
        }
    }
}
