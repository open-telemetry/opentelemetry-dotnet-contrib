// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Owin;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Owin.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Owin;

/// <summary>
/// Instruments incoming request with <see cref="Activity"/> and notifies listeners with <see cref="ActivitySource"/>.
/// </summary>
internal sealed class DiagnosticsMiddleware : OwinMiddleware
{
    private const string ContextKey = "__OpenTelemetry.Context__";
    private static readonly Func<IOwinRequest, string, IEnumerable<string>> OwinRequestHeaderValuesGetter
        = (request, name) => request.Headers.GetValues(name);

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsMiddleware"/> class.
    /// </summary>
    /// <param name="next">An optional pointer to the next component.</param>
    public DiagnosticsMiddleware(OwinMiddleware next)
        : base(next)
    {
    }

    /// <inheritdoc />
    public override async Task Invoke(IOwinContext owinContext)
    {
        long startTimestamp = -1;

        try
        {
            BeginRequest(owinContext);

            if (OwinInstrumentationMetrics.HttpServerDuration.Enabled && !owinContext.Environment.ContainsKey(ContextKey))
            {
                startTimestamp = Stopwatch.GetTimestamp();
            }

            await this.Next.Invoke(owinContext).ConfigureAwait(false);
            RequestEnd(owinContext, null, startTimestamp);
        }
        catch (Exception ex)
        {
            RequestEnd(owinContext, ex, startTimestamp);
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BeginRequest(IOwinContext owinContext)
    {
        try
        {
            if (OwinInstrumentationActivitySource.Options == null || OwinInstrumentationActivitySource.Options.Filter?.Invoke(owinContext) == false)
            {
                OwinInstrumentationEventSource.Log.RequestIsFilteredOut();
                return;
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            OwinInstrumentationEventSource.Log.RequestFilterException(ex);
            return;
        }

        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        var ctx = textMapPropagator.Extract(default, owinContext.Request, OwinRequestHeaderValuesGetter);

        Activity? activity = OwinInstrumentationActivitySource.ActivitySource.StartActivity(
            OwinInstrumentationActivitySource.IncomingRequestActivityName,
            ActivityKind.Server,
            ctx.ActivityContext);

        if (activity != null)
        {
            var request = owinContext.Request;

            /*
             * Note: Display name is intentionally set to a low cardinality
             * value because OWIN does not expose any kind of
             * route/template. See:
             * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/http.md#name
             */
            activity.DisplayName = request.Method switch
            {
                "GET" => "HTTP GET",
                "POST" => "HTTP POST",
                "PUT" => "HTTP PUT",
                "DELETE" => "HTTP DELETE",
                _ => $"HTTP {request.Method}",
            };

            if (activity.IsAllDataRequested)
            {
                if (request.Uri.Port == 80 || request.Uri.Port == 443)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Uri.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeHttpHost, request.Uri.Host + ":" + request.Uri.Port);
                }

                activity.SetTag(SemanticConventions.AttributeHttpMethod, request.Method);
                activity.SetTag(SemanticConventions.AttributeHttpTarget, request.Uri.AbsolutePath);
                activity.SetTag(SemanticConventions.AttributeHttpUrl, GetUriTagValueFromRequestUri(request.Uri, OwinInstrumentationActivitySource.Options.DisableUrlQueryRedaction));

                if (request.Headers.TryGetValue("User-Agent", out string[] userAgent) && userAgent.Length > 0)
                {
                    activity.SetTag(SemanticConventions.AttributeHttpUserAgent, userAgent[0]);
                }

                try
                {
                    OwinInstrumentationActivitySource.Options?.Enrich?.Invoke(
                        activity,
                        OwinEnrichEventType.BeginRequest,
                        owinContext,
                        null);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    OwinInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            if (!(textMapPropagator is TraceContextPropagator))
            {
                Baggage.Current = ctx.Baggage;
            }

            owinContext.Environment[ContextKey] = activity;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RequestEnd(IOwinContext owinContext, Exception? exception, long startTimestamp)
    {
        if (owinContext.Environment.TryGetValue(ContextKey, out object context)
            && context is Activity activity)
        {
            if (Activity.Current != activity)
            {
                Activity.Current = activity;
            }

            if (activity.IsAllDataRequested)
            {
                var response = owinContext.Response;

                if (exception != null)
                {
                    activity.SetStatus(Status.Error);

                    if (OwinInstrumentationActivitySource.Options != null && OwinInstrumentationActivitySource.Options.RecordException)
                    {
                        activity.RecordException(exception);
                    }
                }
                else if (activity.GetStatus().StatusCode == StatusCode.Unset)
                {
                    activity.SetStatus(SpanHelper.ResolveActivityStatusForHttpStatusCode(activity.Kind, response.StatusCode));
                }

                activity.SetTag(SemanticConventions.AttributeHttpStatusCode, response.StatusCode);

                try
                {
                    OwinInstrumentationActivitySource.Options?.Enrich?.Invoke(
                        activity,
                        OwinEnrichEventType.EndRequest,
                        owinContext,
                        exception);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    OwinInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            activity.Stop();

            if (OwinInstrumentationMetrics.HttpServerDuration.Enabled)
            {
                OwinInstrumentationMetrics.HttpServerDuration.Record(
                    activity.Duration.TotalSeconds,
                    new(SemanticConventions.AttributeHttpRequestMethod, owinContext.Request.Method),
                    new(SemanticConventions.AttributeUrlScheme, owinContext.Request.Scheme),
                    new(SemanticConventions.AttributeHttpResponseStatusCode, owinContext.Response.StatusCode));
            }

            if (!(Propagators.DefaultTextMapPropagator is TraceContextPropagator))
            {
                Baggage.Current = default;
            }
        }
        else if (OwinInstrumentationMetrics.HttpServerDuration.Enabled)
        {
            var endTimestamp = Stopwatch.GetTimestamp();
            var duration = endTimestamp - startTimestamp;
            var durationS = duration / (double)Stopwatch.Frequency;

            OwinInstrumentationMetrics.HttpServerDuration.Record(
                durationS,
                new(SemanticConventions.AttributeHttpRequestMethod, owinContext.Request.Method),
                new(SemanticConventions.AttributeUrlScheme, owinContext.Request.Scheme),
                new(SemanticConventions.AttributeHttpResponseStatusCode, owinContext.Response.StatusCode));
        }
    }

    /// <summary>
    /// Gets the OpenTelemetry standard uri tag value for a span based on its request <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri"><see cref="Uri"/>.</param>
    /// <returns>Span uri value.</returns>
    private static string GetUriTagValueFromRequestUri(Uri uri, bool disableQueryRedaction)
    {
        if (string.IsNullOrEmpty(uri.UserInfo) && disableQueryRedaction)
        {
            return uri.OriginalString;
        }

        var query = disableQueryRedaction ? uri.Query : RedactionHelper.GetRedactedQueryString(uri.Query);

        return string.Concat(uri.Scheme, Uri.SchemeDelimiter, uri.Authority, uri.AbsolutePath, query, uri.Fragment);
    }
}
