// <copyright file="HttpInMetricsListener.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Web;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class HttpInMetricsListener : IDisposable
{
    private const string NetworkProtocolName = "http";
    private readonly HttpRequestRouteHelper routeHelper = new();
    private readonly Histogram<double> httpServerDuration;
    private readonly AspNetMetricsInstrumentationOptions options;

    public HttpInMetricsListener(Meter meter, AspNetMetricsInstrumentationOptions options)
    {
        this.httpServerDuration = meter.CreateHistogram<double>(
            "http.server.request.duration",
            unit: "s",
            description: "Measures the duration of inbound HTTP requests.");
        TelemetryHttpModule.Options.OnRequestStoppedCallback += this.OnStopActivity;
        this.options = options;
    }

    public void Dispose()
    {
        TelemetryHttpModule.Options.OnRequestStoppedCallback -= this.OnStopActivity;
    }

    private static string GetHttpProtocolVersion(HttpRequest request)
    {
        var protocol = request.ServerVariables["SERVER_PROTOCOL"];
        return protocol switch
        {
            "HTTP/1.1" => "1.1",
            "HTTP/2" => "2",
            "HTTP/3" => "3",
            _ => protocol,
        };
    }

    private void OnStopActivity(Activity activity, HttpContext context)
    {
        var request = context.Request;
        var url = request.Url;
        var tags = new TagList
        {
            { SemanticConventions.AttributeServerAddress, url.Host },
            { SemanticConventions.AttributeServerPort, url.Port },
            { SemanticConventions.AttributeNetworkProtocolName, NetworkProtocolName },
            { SemanticConventions.AttributeHttpRequestMethod, request.HttpMethod },
            { SemanticConventions.AttributeUrlScheme, url.Scheme },
            { SemanticConventions.AttributeHttpResponseStatusCode, context.Response.StatusCode },
        };

        var protocolVersion = GetHttpProtocolVersion(request);
        if (!string.IsNullOrEmpty(protocolVersion))
        {
            tags.Add(SemanticConventions.AttributeNetworkProtocolVersion, protocolVersion);
        }

        var template = this.routeHelper.GetRouteTemplate(request);
        if (!string.IsNullOrEmpty(template))
        {
            tags.Add(SemanticConventions.AttributeHttpRoute, template);
        }

        if (this.options.Enrich is not null)
        {
            try
            {
                this.options.Enrich(context, ref tags);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInMetricsListener), ex);
            }
        }

        this.httpServerDuration.Record(activity.Duration.TotalSeconds, tags);
    }
}
