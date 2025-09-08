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
    private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
    private readonly HttpRequestRouteHelper routeHelper = new();
    private readonly RequestDataHelper requestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: true);
    private readonly AsyncLocal<long> beginTimestamp = new();

    public HttpInListener()
    {
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

    internal static double CalculateDurationFromTimestamp(long begin, long? end = null)
    {
        end ??= Stopwatch.GetTimestamp();
        var delta = end - begin;
        var ticks = (long)(TimestampToTicks * delta);
        var duration = new TimeSpan(ticks);
        return duration.TotalSeconds;
    }

    private void RecordDuration(Activity? activity, HttpContext context, Exception? exception = null)
    {
        if (AspNetInstrumentation.Instance.HandleManager.MetricHandles == 0)
        {
            return;
        }

        var options = AspNetInstrumentation.Instance.MetricOptions;
        var request = context.Request;
        var url = request.Url;
        var tags = new TagList
        {
            { SemanticConventions.AttributeUrlScheme, url.Scheme },
            { SemanticConventions.AttributeHttpResponseStatusCode, context.Response.StatusCode },
        };

        // Add exception-related tags for metrics when an exception occurred
        if (exception != null)
        {
            tags.Add(SemanticConventions.AttributeErrorType, exception.GetType().FullName);
        }

        var normalizedMethod = this.requestDataHelper.GetNormalizedHttpMethod(request.HttpMethod);
        tags.Add(SemanticConventions.AttributeHttpRequestMethod, normalizedMethod);

        if (options.EnableServerAttributesForRequestDuration)
        {
            tags.Add(SemanticConventions.AttributeServerAddress, url.Host);
            tags.Add(SemanticConventions.AttributeServerPort, url.Port);
        }

        var protocolVersion = RequestDataHelperExtensions.GetHttpProtocolVersion(request);
        if (!string.IsNullOrEmpty(protocolVersion))
        {
            // Determine the actual protocol name from the request
            var protocolName = url.Scheme ?? Uri.UriSchemeHttp;

            // Only add network.protocol.name when it's not "http" and version is available
            // Per spec: "Conditionally Required: If not http and network.protocol.version is set."
            if (!string.Equals(protocolName, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                tags.Add(SemanticConventions.AttributeNetworkProtocolName, protocolName);
            }

            tags.Add(SemanticConventions.AttributeNetworkProtocolVersion, protocolVersion);
        }

        var template = this.routeHelper.GetRouteTemplate(request);
        if (!string.IsNullOrEmpty(template))
        {
            tags.Add(SemanticConventions.AttributeHttpRoute, template);
        }

        if (options.EnrichWithHttpContext is not null)
        {
            try
            {
                options.EnrichWithHttpContext(context, ref tags);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), ex);
            }
        }

        var duration = activity?.Duration.TotalSeconds ??
            CalculateDurationFromTimestamp(this.beginTimestamp.Value);
        AspNetInstrumentation.HttpServerDuration.Record(duration, tags);
    }

    private void OnStartActivity(Activity? activity, HttpContext context)
    {
        if (AspNetInstrumentation.Instance.HandleManager.TracingHandles == 0)
        {
            if (AspNetInstrumentation.Instance.HandleManager.MetricHandles > 0)
            {
                // If we are not tracing, but we are collecting metrics, we still
                // need to set the activity name and tags.
                this.beginTimestamp.Value = Stopwatch.GetTimestamp();
            }

            return;
        }

        if (activity == null)
        {
            AspNetInstrumentationEventSource.Log.NullActivity(nameof(this.OnStartActivity));
            return;
        }

        var options = AspNetInstrumentation.Instance.TraceOptions;

        if (activity.IsAllDataRequested)
        {
            try
            {
                // TODO Ideally we would also check
                // Sdk.SuppressInstrumentation here to prevent tagging a
                // span that will not be collected, but we can't do that
                // without an SDK reference. Need the spec to come around on
                // this.

                if (options.Filter?.Invoke(context) == false)
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

            var query = url.Query;
            if (!string.IsNullOrEmpty(query))
            {
                var queryString = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
                activity.SetTag(SemanticConventions.AttributeUrlQuery, options.DisableUrlQueryRedaction ? queryString : RedactionHelper.GetRedactedQueryString(queryString));
            }

            var userAgent = request.UserAgent;
            if (!string.IsNullOrEmpty(userAgent))
            {
                activity.SetTag(SemanticConventions.AttributeUserAgentOriginal, userAgent);
            }

            try
            {
                options.EnrichWithHttpRequest?.Invoke(activity, request);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnStartActivity", ex);
            }
        }
    }

    private void OnStopActivity(Activity? activity, HttpContext context)
    {
        if (AspNetInstrumentation.Instance.HandleManager.TracingHandles == 0)
        {
            this.RecordDuration(activity, context);
            return;
        }

        if (activity == null)
        {
            AspNetInstrumentationEventSource.Log.NullActivity(nameof(this.OnStopActivity));
            this.RecordDuration(activity, context);
            return;
        }

        var options = AspNetInstrumentation.Instance.TraceOptions;

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
                options.EnrichWithHttpResponse?.Invoke(activity, response);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnStopActivity", ex);
            }
        }

        this.RecordDuration(activity, context);
    }

    private void OnException(Activity? activity, HttpContext context, Exception exception)
    {
        if (AspNetInstrumentation.Instance.HandleManager.TracingHandles == 0)
        {
            this.RecordDuration(activity, context, exception);
            return;
        }

        if (activity == null)
        {
            AspNetInstrumentationEventSource.Log.NullActivity(nameof(this.OnException));
            this.RecordDuration(activity, context, exception);
            return;
        }

        var options = AspNetInstrumentation.Instance.TraceOptions;

        if (activity.IsAllDataRequested)
        {
            if (options.RecordException)
            {
                activity.AddException(exception);
            }

            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag(SemanticConventions.AttributeErrorType, exception.GetType().FullName);

            try
            {
                options.EnrichWithException?.Invoke(activity, exception);
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.EnrichmentException("OnException", ex);
            }
        }

        this.RecordDuration(activity, context, exception);
    }
}
