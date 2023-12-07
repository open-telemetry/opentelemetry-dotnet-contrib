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
    private readonly Histogram<double> httpServerDuration;
    private readonly AspNetMetricsInstrumentationOptions options;

    public HttpInMetricsListener(Meter meter, AspNetMetricsInstrumentationOptions options)
    {
        this.httpServerDuration = meter.CreateHistogram<double>("http.server.duration", "ms", "Measures the duration of inbound HTTP requests.");
        TelemetryHttpModule.Options.OnRequestStoppedCallback += this.OnStopActivity;
        this.options = options;
    }

    public void Dispose()
    {
        TelemetryHttpModule.Options.OnRequestStoppedCallback -= this.OnStopActivity;
    }

    private void OnStopActivity(Activity activity, HttpContext context)
    {
        // TODO: This is just a minimal set of attributes. See the spec for additional attributes:
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/http-metrics.md#http-server
        var tags = new TagList
        {
            { SemanticConventions.AttributeHttpMethod, context.Request.HttpMethod },
            { SemanticConventions.AttributeHttpScheme, context.Request.Url.Scheme },
            { SemanticConventions.AttributeHttpStatusCode, context.Response.StatusCode },
        };

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

        this.httpServerDuration.Record(activity.Duration.TotalMilliseconds, tags);
    }
}
