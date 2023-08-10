// <copyright file="AWSLambdaHttpUtils.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Web;
using Amazon.Lambda.APIGatewayEvents;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AWSLambda.Implementation;

internal class AWSLambdaHttpUtils
{
    // x-forwarded-... headers are described here https://docs.aws.amazon.com/elasticloadbalancing/latest/classic/x-forwarded-headers.html
    private const string HeaderXForwardedProto = "x-forwarded-proto";
    private const string HeaderHost = "host";

    internal static IEnumerable<KeyValuePair<string, object>> GetHttpTags<TInput>(TInput input)
    {
        var tags = new List<KeyValuePair<string, object>>();

        string? httpScheme = null;
        string? httpTarget = null;
        string? httpMethod = null;
        string? hostName = null;
        int? hostPort = null;

        switch (input)
        {
            case APIGatewayProxyRequest request:
                httpScheme = AWSLambdaUtils.GetHeaderValues(request, HeaderXForwardedProto)?.LastOrDefault();
                var path = request.RequestContext?.Path ?? request.Path ?? string.Empty;
                httpTarget = string.Concat(path, GetQueryString(request));
                httpMethod = request.RequestContext?.HttpMethod ?? request.HttpMethod;
                var hostHeader = AWSLambdaUtils.GetHeaderValues(request, HeaderHost)?.LastOrDefault();
                (hostName, hostPort) = GetHostAndPort(httpScheme, hostHeader);
                break;
            case APIGatewayHttpApiV2ProxyRequest requestV2:
                httpScheme = AWSLambdaUtils.GetHeaderValues(requestV2, HeaderXForwardedProto)?.LastOrDefault();
                httpTarget = string.Concat(requestV2.RawPath ?? string.Empty, GetQueryString(requestV2));
                httpMethod = requestV2.RequestContext?.Http?.Method;
                var hostHeaderV2 = AWSLambdaUtils.GetHeaderValues(requestV2, HeaderHost)?.LastOrDefault();
                (hostName, hostPort) = GetHostAndPort(httpScheme, hostHeaderV2);
                break;
            default:
                return tags;
        }

        tags.AddTagIfNotNull(SemanticConventions.AttributeHttpScheme, httpScheme);
        tags.AddTagIfNotNull(SemanticConventions.AttributeHttpTarget, httpTarget);
        tags.AddTagIfNotNull(SemanticConventions.AttributeHttpMethod, httpMethod);
        tags.AddTagIfNotNull(SemanticConventions.AttributeNetHostName, hostName);
        tags.AddTagIfNotNull(SemanticConventions.AttributeNetHostPort, hostPort);

        return tags;
    }

    internal static void SetHttpTagsFromResult(Activity? activity, object? result)
    {
        if (activity == null || result == null)
        {
            return;
        }

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

    internal static string? GetQueryString(APIGatewayProxyRequest request)
    {
        if (request.MultiValueQueryStringParameters == null)
        {
            return string.Empty;
        }

        var queryString = new StringBuilder();
        var separator = '?';
        foreach (var parameterKvp in request.MultiValueQueryStringParameters)
        {
            // Multiple values for the same parameter will be added to query
            // as ampersand separated: name=value1&name=value2
            foreach (var value in parameterKvp.Value)
            {
                queryString.Append(separator)
                    .Append(HttpUtility.UrlEncode(parameterKvp.Key))
                    .Append('=')
                    .Append(HttpUtility.UrlEncode(value));
                separator = '&';
            }
        }

        return queryString.ToString();
    }

    internal static string? GetQueryString(APIGatewayHttpApiV2ProxyRequest request) =>
        string.IsNullOrEmpty(request.RawQueryString) ? string.Empty : "?" + request.RawQueryString;

    internal static (string? Host, int? Port) GetHostAndPort(string? httpScheme, string? hostHeader)
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

    private static int? GetDefaultPort(string? httpScheme) =>
        httpScheme == "https" ? 443 : httpScheme == "http" ? 80 : null;
}
