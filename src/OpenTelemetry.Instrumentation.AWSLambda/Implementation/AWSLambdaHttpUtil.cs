// <copyright file="AWSLambdaHttpUtil.cs" company="OpenTelemetry Authors">
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
using System.Web;
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation
{
    internal class AWSLambdaHttpUtil
    {
        // x-forwarded-... headers are described here https://docs.aws.amazon.com/elasticloadbalancing/latest/classic/x-forwarded-headers.html
        private const string HeaderXForwardedProto = "x-forwarded-proto";
        private const string HeaderHost = "host";

        internal static IEnumerable<KeyValuePair<string, object>> GetHttpTags<TInput>(TInput input)
        {
            var tags = new List<KeyValuePair<string, object>>();

            string httpScheme = null;
            string httpTarget = null;
            string httpMethod = null;
            string hostName = null;
            int? hostPort = null;

            switch (input)
            {
                case APIGatewayProxyRequest request:
                    httpScheme = AWSLambdaUtils.GetHeaderValues(request, HeaderXForwardedProto)?.FirstOrDefault();
                    httpTarget = string.Concat(request.Path ?? string.Empty, ConstructQueryString(request.QueryStringParameters));
                    httpMethod = request.HttpMethod;
                    var hostHeader = AWSLambdaUtils.GetHeaderValues(request, HeaderHost)?.FirstOrDefault();
                    (hostName, hostPort) = GetHostAndPort(httpScheme, hostHeader);
                    break;
                case APIGatewayHttpApiV2ProxyRequest requestV2:
                    httpScheme = AWSLambdaUtils.GetHeaderValues(requestV2, HeaderXForwardedProto)?.FirstOrDefault();
                    httpTarget = string.Concat(requestV2?.RequestContext?.Http?.Path ?? string.Empty, ConstructQueryString(requestV2.QueryStringParameters));
                    httpMethod = requestV2?.RequestContext?.Http?.Method;
                    var hostHeaderV2 = AWSLambdaUtils.GetHeaderValues(requestV2, HeaderHost)?.FirstOrDefault();
                    (hostName, hostPort) = GetHostAndPort(httpScheme, hostHeaderV2);
                    break;
            }

            tags.AddTagIfNotNull(SemanticConventions.AttributeHttpScheme, httpScheme);
            tags.AddTagIfNotNull(SemanticConventions.AttributeHttpTarget, httpTarget);
            tags.AddTagIfNotNull(SemanticConventions.AttributeHttpMethod, httpMethod);
            tags.AddTagIfNotNull(SemanticConventions.AttributeNetHostName, hostName);
            tags.AddTagIfNotNull(SemanticConventions.AttributeNetHostPort, hostPort);

            return tags;
        }

        internal static void SetHttpTagsFromResult(Activity activity, object result)
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

        internal static (string Host, int? Port) GetHostAndPort(string httpScheme, string hostHeader)
        {
            if (hostHeader == null)
            {
                return (null, null);
            }

            var hostAndPort = hostHeader.Split(new char[] { ':' }, 2);
            if (hostAndPort.Length > 1)
            {
                var host = hostAndPort[0];
                return int.TryParse(hostAndPort[1], out var port)
                    ? (host, port)
                    : (host, null);
            }
            else
            {
                return (hostAndPort[0], GetDefaultPort(httpScheme));
            }
        }

        private static int? GetDefaultPort(string httpScheme)
        {
            int? defaultPort = null;
            switch (httpScheme)
            {
                case "http":
                    defaultPort = 80;
                    break;
                case "https":
                    defaultPort = 443;
                    break;
            }

            return defaultPort;
        }

        private static string ConstructQueryString(IDictionary<string, string> requestParameters)
        {
            if (requestParameters == null || requestParameters.Count == 0)
            {
                return string.Empty;
            }

            var items = new List<string>();
            foreach (string name in requestParameters.Keys)
            {
                items.Add(string.Concat(name, "=", HttpUtility.UrlEncode(requestParameters[name])));
            }

            return string.Concat("?", string.Join("&", items.ToArray()));
        }
    }
}
