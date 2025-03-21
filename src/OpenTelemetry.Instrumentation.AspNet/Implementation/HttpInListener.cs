// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    private readonly RequestDataHelper requestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: true);

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

            // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.24.0/docs/http/http-spans.md
            var originalHttpMethod = request.HttpMethod;
            this.requestDataHelper.SetActivityDisplayName(activity, originalHttpMethod);

            var url = request.Url;
            activity.SetTag(SemanticConventions.AttributeServerAddress, url.Host);
            activity.SetTag(SemanticConventions.AttributeServerPort, url.Port);
            activity.SetTag(SemanticConventions.AttributeUrlScheme, url.Scheme);

            this.requestDataHelper.SetHttpMethodTag(activity, originalHttpMethod);

            var protocolVersion = RequestDataHelperExtensions.GetHttpProtocolVersion(request);
            if (!string.IsNullOrEmpty(protocolVersion))
            {
                activity.SetTag(SemanticConventions.AttributeNetworkProtocolVersion, protocolVersion);
            }

            // TODO url.query should be sanitized
            var query = url.Query;
            if (!string.IsNullOrEmpty(query))
            {
                var queryString = query.StartsWith("?", StringComparison.InvariantCulture) ? query.Substring(1) : query;
                activity.SetTag(SemanticConventions.AttributeUrlQuery, this.options.DisableUrlQueryRedaction ? queryString : RedactionHelper.GetRedactedQueryString(queryString));
            }

            var userAgent = request.UserAgent;
            if (!string.IsNullOrEmpty(userAgent))
            {
                activity.SetTag(SemanticConventions.AttributeUserAgentOriginal, userAgent);
            }

            try
            {
                this.options.EnrichWithHttpRequest?.Invoke(activity, request);
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

            activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, response.StatusCode);

            if (activity.Status == ActivityStatusCode.Unset)
            {
                activity.SetStatus(SpanHelper.ResolveActivityStatusForHttpStatusCode(activity.Kind, response.StatusCode));
            }

            var template = this.routeHelper.GetRouteTemplate(context.Request);

            if (!string.IsNullOrEmpty(template))
            {
                // Override the name that was previously set to the normalized HTTP method/HTTP
                this.requestDataHelper.SetActivityDisplayName(activity, context.Request.HttpMethod, template);
                activity.SetTag(SemanticConventions.AttributeHttpRoute, template);
            }

            try
            {
                this.options.EnrichWithHttpResponse?.Invoke(activity, response);
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
                activity.AddException(exception);
            }

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag(SemanticConventions.AttributeErrorType, exception.GetType().FullName);

            try
            {
                this.options.EnrichWithException?.Invoke(activity, exception);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnException", ex);
            }
        }
    }
}
