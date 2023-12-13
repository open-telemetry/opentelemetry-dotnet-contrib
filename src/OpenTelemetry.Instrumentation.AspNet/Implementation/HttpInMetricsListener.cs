// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Web;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class HttpInMetricsListener : IDisposable
{
    private readonly HttpRequestRouteHelper routeHelper = new();
    private readonly RequestMethodHelper requestMethodHelper = new();
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
            { SemanticConventions.AttributeUrlScheme, url.Scheme },
            { SemanticConventions.AttributeHttpResponseStatusCode, context.Response.StatusCode },
        };

        var normalizedMethod = this.requestMethodHelper.GetNormalizedHttpMethod(request.HttpMethod);
        tags.Add(SemanticConventions.AttributeHttpRequestMethod, normalizedMethod);

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
