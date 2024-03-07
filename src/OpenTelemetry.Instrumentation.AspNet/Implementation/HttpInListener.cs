// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation;

internal sealed class HttpInListener : IDisposable
{
    private readonly HttpRequestRouteHelper routeHelper = new();
    private readonly AspNetTraceInstrumentationOptions options;

    public HttpInListener(AspNetTraceInstrumentationOptions options)
    {
        Guard.ThrowIfNull(options);

        this.options = options;

        TelemetryHttpModule.Options.TextMapPropagator = Propagators.DefaultTextMapPropagator;

        TelemetryHttpModule.Options.OnRequestStartedCallback += this.OnStartActivity;
        TelemetryHttpModule.Options.OnRequestStoppedCallback += this.OnStopActivity;
        TelemetryHttpModule.Options.OnExceptionCallback += this.OnException;
    }

    public void Dispose()
    {
        TelemetryHttpModule.Options.OnRequestStartedCallback -= this.OnStartActivity;
        TelemetryHttpModule.Options.OnRequestStoppedCallback -= this.OnStopActivity;
        TelemetryHttpModule.Options.OnExceptionCallback -= this.OnException;
    }

    /// <summary>
    /// Gets the OpenTelemetry standard uri tag value for a span based on its request <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri"><see cref="Uri"/>.</param>
    /// <returns>Span uri value.</returns>
    private static string GetUriTagValueFromRequestUri(Uri uri)
    {
        return string.IsNullOrEmpty(uri.UserInfo) ? uri.ToString() : string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.PathAndQuery, uri.Fragment);
    }

    private void OnStartActivity(Activity activity, HttpContext context)
    {
        if (activity.IsAllDataRequested)
        {
            try
            {
                // todo: Ideally we would also check
                // Sdk.SuppressInstrumentation here to prevent tagging a
                // span that will not be collected but we can't do that
                // without an SDK reference. Need the spec to come around on
                // this.

                if (this.options.Filter?.Invoke(context) == false)
                {
                    AspNetInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    return;
                }
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.RequestFilterException(activity.OperationName, ex);
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                return;
            }

            var request = context.Request;
            var requestValues = request.Unvalidated;

            // see the spec https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md
            var path = requestValues.Path;
            activity.DisplayName = path;

            if (request.Url.Port == 80 || request.Url.Port == 443)
            {
                activity.SetTag(SemanticConventions.AttributeHttpHost, request.Url.Host);
            }
            else
            {
                activity.SetTag(SemanticConventions.AttributeHttpHost, request.Url.Host + ":" + request.Url.Port);
            }

            activity.SetTag(SemanticConventions.AttributeHttpMethod, request.HttpMethod);
            activity.SetTag(SemanticConventions.AttributeHttpTarget, path);
            activity.SetTag(SemanticConventions.AttributeHttpUserAgent, request.UserAgent);
            activity.SetTag(SemanticConventions.AttributeHttpUrl, GetUriTagValueFromRequestUri(request.Url));

            try
            {
                this.options.Enrich?.Invoke(activity, "OnStartActivity", request);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnStartActivity", ex);
            }
        }
    }

    private void OnStopActivity(Activity activity, HttpContext context)
    {
        if (activity.IsAllDataRequested)
        {
            var response = context.Response;

            activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

            if (activity.Status == ActivityStatusCode.Unset)
            {
                activity.SetStatus(SpanHelper.ResolveActivityStatusForHttpStatusCode(activity.Kind, response.StatusCode));
            }

            var template = this.routeHelper.GetRouteTemplate(context.Request);

            if (!string.IsNullOrEmpty(template))
            {
                // Override the name that was previously set to the path part of URL.
                activity.DisplayName = template!;
                activity.SetTag(SemanticConventions.AttributeHttpRoute, template);
            }

            try
            {
                this.options.Enrich?.Invoke(activity, "OnStopActivity", response);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnStopActivity", ex);
            }
        }
    }

    private void OnException(Activity activity, HttpContext context, Exception exception)
    {
        if (activity.IsAllDataRequested)
        {
            if (this.options.RecordException)
            {
                activity.RecordException(exception);
            }

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);

            try
            {
                this.options.Enrich?.Invoke(activity, "OnException", exception);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnException", ex);
            }
        }
    }
}
