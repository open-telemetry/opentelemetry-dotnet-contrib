// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Web;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class HttpInMetricsListener : IDisposable
{
    private readonly HttpRequestRouteHelper routeHelper = new();
    private readonly RequestDataHelper requestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: true);
    private readonly Histogram<double> httpServerDuration;
    private readonly AspNetMetricsInstrumentationOptions options;

    public HttpInMetricsListener(Meter meter, AspNetMetricsInstrumentationOptions options)
    {
        this.httpServerDuration = meter.CreateHistogram<double>(
            "http.server.request.duration",
            unit: "s",
            description: "Duration of HTTP server requests.");
        TelemetryHttpModule.Options.OnRequestStoppedCallback += this.OnStopActivity;
        this.options = options;
    }

    public void Dispose()
    {
        TelemetryHttpModule.Options.OnRequestStoppedCallback -= this.OnStopActivity;
    }

    private void OnStopActivity(Activity activity, HttpContext context)
    {
        var request = context.Request;
        var url = request.Url;
        var tags = new TagList
        {
            { SemanticConventions.AttributeUrlScheme, url.Scheme },
            { SemanticConventions.AttributeHttpResponseStatusCode, context.Response.StatusCode },
        };

        if (this.options.EnableServerAttributesForRequestDuration)
        {
            tags.Add(SemanticConventions.AttributeServerAddress, url.Host);
            tags.Add(SemanticConventions.AttributeServerPort, url.Port);
        }

        var normalizedMethod = this.requestDataHelper.GetNormalizedHttpMethod(request.HttpMethod);
        tags.Add(SemanticConventions.AttributeHttpRequestMethod, normalizedMethod);

        var protocolVersion = RequestDataHelperExtensions.GetHttpProtocolVersion(request);
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
