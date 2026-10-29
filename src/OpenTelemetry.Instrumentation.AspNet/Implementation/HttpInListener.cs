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
    private const string StopInstrumentationCallbackContextKey = "__AspnetOpenTelemetryInstrumentationStopCallback__";
    private static readonly object ErrorTypeHttpContextItemsKey = new();
    private static readonly object MetricStartTimestampHttpContextItemsKey = new();

    private readonly HttpRequestRouteHelper routeHelper = new();
    private readonly RequestDataHelper requestDataHelper = new(configureByHttpKnownMethodsEnvironmentalVariable: true);
    private readonly Action<Activity?, HttpContextBase> onStopActivityCallback;

    public HttpInListener()
    {
        this.onStopActivityCallback = this.OnStopActivity;

        TelemetryHttpModule.Options.TextMapPropagator = Propagators.DefaultTextMapPropagator;

        TelemetryHttpModule.Options.OnRequestStartedCallback += this.StartActivity;
        TelemetryHttpModule.Options.OnExceptionCallback += this.OnException;
    }

    public void Dispose()
    {
        TelemetryHttpModule.Options.OnRequestStartedCallback -= this.StartActivity;
        TelemetryHttpModule.Options.OnExceptionCallback -= this.OnException;
    }

    private static void StoreMetricStartTimestamp(HttpContextBase context, long metricStartTimestamp)
    {
        if (metricStartTimestamp != 0)
        {
            context.Items[MetricStartTimestampHttpContextItemsKey] = metricStartTimestamp;
        }
    }

    private void RecordDuration(Activity? activity, HttpContextBase context, HttpRequestBase? request = null, HttpResponseBase? response = null, string? routeTemplate = null)
    {
        double duration;
        if (activity is not null)
        {
            if (AspNetInstrumentation.Instance.HandleManager.MetricHandles == 0)
            {
                return;
            }

            duration = activity.Duration.TotalSeconds;
        }
        else
        {
            // The start timestamp is only stored when no activity was created for the request.
            var metricStartTimestamp = context.Items[MetricStartTimestampHttpContextItemsKey];
            context.Items.Remove(MetricStartTimestampHttpContextItemsKey);

            if (AspNetInstrumentation.Instance.HandleManager.MetricHandles == 0 || metricStartTimestamp is not long beginTimestamp)
            {
                return;
            }

            duration = Stopwatch.GetElapsedTime(beginTimestamp).TotalSeconds;
        }

        // The wrappers are only read once it is known the duration will be recorded, as reading them allocates.
        request ??= context.Request;
        response ??= context.Response;

        var options = AspNetInstrumentation.Instance.MetricOptions;
        var url = request.Url;
        var tags = new TagList
        {
            { SemanticConventions.AttributeUrlScheme, url.Scheme },
            { SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(response.StatusCode) },
        };

        if (context.Items[ErrorTypeHttpContextItemsKey] is string errorType)
        {
            tags.Add(SemanticConventions.AttributeErrorType, errorType);
        }

        var normalizedMethod = this.requestDataHelper.GetNormalizedHttpMethod(request.HttpMethod);
        tags.Add(SemanticConventions.AttributeHttpRequestMethod, normalizedMethod);

        if (options.EnableServerAttributesForRequestDuration)
        {
            tags.Add(SemanticConventions.AttributeServerAddress, url.Host);
            tags.Add(SemanticConventions.AttributeServerPort, PortTelemetryHelper.GetBoxedPort(url.Port, cacheValue: true));
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

        var template = routeTemplate ?? this.routeHelper.GetRouteTemplate(request);
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

        AspNetInstrumentation.HttpServerDuration.Record(duration, tags);
    }

    private Activity? StartActivity(HttpContextBase context, ActivityContext activityContext)
    {
        context.Items[StopInstrumentationCallbackContextKey] = this.onStopActivityCallback;

        // When an activity is created its duration is used for the metric, so the start timestamp
        // is only boxed and stored in the HttpContext when there is no activity to provide it.
        var handleManager = AspNetInstrumentation.Instance.HandleManager;
        var metricStartTimestamp = handleManager.MetricHandles > 0 ? Stopwatch.GetTimestamp() : 0L;

        if (handleManager.TracingHandles == 0)
        {
            StoreMetricStartTimestamp(context, metricStartTimestamp);
            return null;
        }

        TagList tags = default;

        var request = context.Request;
        var originalHttpMethod = request.HttpMethod;
        var activityName = this.requestDataHelper.GetActivityDisplayName(originalHttpMethod);
        this.requestDataHelper.SetHttpMethodTag(ref tags, originalHttpMethod);

        var url = request.Url;
        tags.Add(SemanticConventions.AttributeServerAddress, url.Host);
        tags.Add(SemanticConventions.AttributeServerPort, PortTelemetryHelper.GetBoxedPort(url.Port, cacheValue: true));
        tags.Add(SemanticConventions.AttributeUrlScheme, url.Scheme);

        if (request.Unvalidated?.Path is string path)
        {
            tags.Add(SemanticConventions.AttributeUrlPath, path);
        }

        var userAgent = request.UserAgent;
        if (!string.IsNullOrEmpty(userAgent))
        {
            tags.Add(SemanticConventions.AttributeUserAgentOriginal, userAgent);
        }

        var query = url.Query;
        var options = AspNetInstrumentation.Instance.TraceOptions;
        if (!string.IsNullOrEmpty(query))
        {
            var queryString = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
            tags.Add(SemanticConventions.AttributeUrlQuery, options.DisableUrlQueryRedaction ? queryString : RedactionHelper.GetRedactedQueryString(queryString));
        }

        var activity = AspNetInstrumentation.ActivitySource.StartActivity(activityName, ActivityKind.Server, activityContext, tags);

        if (activity == null)
        {
            AspNetInstrumentationEventSource.Log.NullActivity(nameof(this.StartActivity));
            StoreMetricStartTimestamp(context, metricStartTimestamp);
            return null;
        }

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
                    return activity;
                }
            }
            catch (Exception ex)
            {
                AspNetInstrumentationEventSource.Log.RequestFilterException(activity.OperationName, ex);
                activity.IsAllDataRequested = false;
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                return activity;
            }

            var protocolVersion = RequestDataHelperExtensions.GetHttpProtocolVersion(request);
            if (!string.IsNullOrEmpty(protocolVersion))
            {
                activity.SetTag(SemanticConventions.AttributeNetworkProtocolVersion, protocolVersion);
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

        return activity;
    }

    private void OnStopActivity(Activity? activity, HttpContextBase context)
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
        string? template = null;
        HttpRequestBase? request = null;
        HttpResponseBase? response = null;

        if (activity.IsAllDataRequested)
        {
            // HttpContextBase.Request and HttpContextBase.Response allocate a new wrapper on each access, so read them once.
            request = context.Request;
            response = context.Response;

            activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(response.StatusCode));

            if (activity.Status == ActivityStatusCode.Unset)
            {
                activity.SetStatus(SpanHelper.ResolveActivityStatusForHttpStatusCode(activity.Kind, response.StatusCode));
            }

            template = this.routeHelper.GetRouteTemplate(request);

            if (!string.IsNullOrEmpty(template))
            {
                // Override the name that was previously set to the normalized HTTP method/HTTP
                this.requestDataHelper.SetActivityDisplayName(activity, request.HttpMethod, template);
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

        this.RecordDuration(activity, context, request, response, template);
    }

    private void OnException(Activity? activity, HttpContextBase context, Exception exception)
    {
        context.Items[ErrorTypeHttpContextItemsKey] = exception.GetType().FullName;

        if (AspNetInstrumentation.Instance.HandleManager.TracingHandles == 0)
        {
            return;
        }

        if (activity == null)
        {
            AspNetInstrumentationEventSource.Log.NullActivity(nameof(this.OnException));
            return;
        }

        var options = AspNetInstrumentation.Instance.TraceOptions;

        if (activity.IsAllDataRequested)
        {
            if (options.RecordException)
            {
                activity.AddException(exception);
            }

            activity.SetStatus(ActivityStatusCode.Error);
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
    }
}
