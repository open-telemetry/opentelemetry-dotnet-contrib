// <copyright file="CustomWebResponse.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Amazon.Runtime.Internal.Transform;

namespace OpenTelemetry.Instrumentation.AWS.Tests.Tools;

internal class CustomWebResponse : IWebResponseData
{
    private HttpResponseMessageBody response;
    private string[]? headerNames;
    private Dictionary<string, string?>? headers;
    private HashSet<string>? headerNamesSet;

    public CustomWebResponse(HttpResponseMessage response)
        : this(response, null, false)
    {
    }

    public CustomWebResponse(HttpResponseMessage responseMsg, HttpClient? httpClient, bool disposeClient)
    {
        this.response = new HttpResponseMessageBody(responseMsg, httpClient, disposeClient);

        this.StatusCode = responseMsg.StatusCode;
        this.IsSuccessStatusCode = responseMsg.IsSuccessStatusCode;
        this.ContentLength = responseMsg.Content.Headers.ContentLength ?? 0;
        this.CopyHeaderValues(responseMsg);
    }

    public HttpStatusCode StatusCode { get; private set; }

    public bool IsSuccessStatusCode { get; private set; }

    public string? ContentType { get; private set; }

    public long ContentLength { get; private set; }

    public IHttpResponseBody ResponseBody
    {
        get { return this.response; }
    }

    public static IWebResponseData GenerateWebResponse(HttpResponseMessage response)
    {
        return new CustomWebResponse(response);
    }

    public string? GetHeaderValue(string headerName)
    {
        if (this.headers != null && this.headers.TryGetValue(headerName, out var headerValue))
        {
            return headerValue;
        }

        return string.Empty;
    }

    public bool IsHeaderPresent(string headerName)
    {
        return this.headerNamesSet != null && this.headerNamesSet.Contains(headerName);
    }

    public string[]? GetHeaderNames()
    {
        return this.headerNames;
    }

    private void CopyHeaderValues(HttpResponseMessage response)
    {
        List<string> headerNames = new List<string>();
        this.headers = new Dictionary<string, string?>(10, StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, IEnumerable<string>> kvp in response.Headers)
        {
            headerNames.Add(kvp.Key);
            var headerValue = this.GetFirstHeaderValue(response.Headers, kvp.Key);
            this.headers.Add(kvp.Key, headerValue);
        }

        if (response.Content != null)
        {
            foreach (var kvp in response.Content.Headers)
            {
                if (!headerNames.Contains(kvp.Key))
                {
                    headerNames.Add(kvp.Key);
                    var headerValue = this.GetFirstHeaderValue(response.Content.Headers, kvp.Key);
                    this.headers.Add(kvp.Key, headerValue);
                }
            }
        }

        this.headerNames = headerNames.ToArray();
        this.headerNamesSet = new HashSet<string>(this.headerNames, StringComparer.OrdinalIgnoreCase);
    }

    private string? GetFirstHeaderValue(HttpHeaders headers, string key)
    {
        if (headers.TryGetValues(key, out var headerValues))
        {
            return headerValues.FirstOrDefault();
        }

        return string.Empty;
    }
}
