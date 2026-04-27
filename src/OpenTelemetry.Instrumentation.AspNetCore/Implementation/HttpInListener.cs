// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

internal class HttpInListener : ListenerHandler
{
    internal const string ActivityOperationName = "Microsoft.AspNetCore.Hosting.HttpRequestIn";
    internal const string OnStartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
    internal const string OnStopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    internal const string OnUnhandledHostingExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
    internal const string OnUnHandledDiagnosticsExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";

    // https://github.com/dotnet/aspnetcore/blob/8d6554e655b64da75b71e0e20d6db54a3ba8d2fb/src/Hosting/Hosting/src/GenericHost/GenericWebHostBuilder.cs#L85
    internal const string AspNetCoreActivitySourceName = "Microsoft.AspNetCore";

    internal static readonly AssemblyName AssemblyName = typeof(HttpInListener).Assembly.GetName();
#pragma warning disable IDE0370 // Suppression is unnecessary
    internal static readonly string ActivitySourceName = AssemblyName.Name!;
    internal static readonly Version Version = AssemblyName.Version!;
#pragma warning restore IDE0370 // Suppression is unnecessary
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());
    internal static readonly bool Net7OrGreater = Environment.Version.Major >= 7;

    private const string DiagnosticSourceName = "Microsoft.AspNetCore";
    private const string CreatedByInstrumentationPropertyName = "OpenTelemetry.AspNetCore.CreatedByInstrumentation";

    private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) =>
    {
        if (request.Headers.TryGetValue(name, out var value))
        {
            // This causes allocation as the `StringValues` struct has to be casted to an `IEnumerable<string>` object.
            return value;
        }

        return [];
    };

    private static readonly PropertyFetcher<Exception> ExceptionPropertyFetcher = new("Exception");
    private static readonly object CreatedByInstrumentationMarker = new();

    // Caches the display name, rpc.service, and rpc.method derived from the raw gRPC method string.
    // The set of distinct gRPC method strings is bounded by the number of gRPC endpoints in the app.
    private static readonly GrpcMethodDetailsCache GrpcMethodCache = new();

    private readonly AspNetCoreTraceInstrumentationOptions options;

    public HttpInListener(AspNetCoreTraceInstrumentationOptions options)
        : base(DiagnosticSourceName)
    {
        Guard.ThrowIfNull(options);

        this.options = options;
    }

    public override void OnEventWritten(string name, object? payload)
    {
        var activity = Activity.Current!;

        switch (name)
        {
            case OnStartEvent:
                {
                    this.OnStartActivity(activity, payload);
                }

                break;
            case OnStopEvent:
                {
                    this.OnStopActivity(activity, payload);
                }

                break;
            case OnUnhandledHostingExceptionEvent:
            case OnUnHandledDiagnosticsExceptionEvent:
                {
                    this.OnException(activity, payload);
                }

                break;
            default:
                break;
        }
    }

    public void OnStartActivity(Activity activity, object? payload)
    {
        // The overall flow of what AspNetCore library does is as below:
        // Activity.Start()
        // DiagnosticSource.WriteEvent("Start", payload)
        // DiagnosticSource.WriteEvent("Stop", payload)
        // Activity.Stop()

        // This method is in the WriteEvent("Start", payload) path.
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (payload is not HttpContext context)
        {
            AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName);
            return;
        }

        // Ensure context extraction irrespective of sampling decision
        var request = context.Request;
        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        if (textMapPropagator is not TraceContextPropagator)
        {
            var ctx = textMapPropagator.Extract(default, request, HttpRequestHeaderValuesGetter);
            if (ctx.ActivityContext.IsValid()
                && !((ctx.ActivityContext.TraceId == activity.TraceId)
                    && (ctx.ActivityContext.SpanId == activity.ParentSpanId)
                    && (ctx.ActivityContext.TraceState == activity.TraceStateString)))
            {
                // Create a new activity with its parent set from the extracted context.
                // This makes the new activity as a "sibling" of the activity created by
                // Asp.Net Core.
                Activity? newOne;
                if (Net7OrGreater)
                {
                    // For NET7.0 onwards activity is created using ActivitySource so,
                    // we will use the source of the activity to create the new one.
                    newOne = activity.Source.CreateActivity(ActivityOperationName, ActivityKind.Server, ctx.ActivityContext);
                }
                else
                {
#pragma warning disable CA2000
                    newOne = new Activity(ActivityOperationName);
#pragma warning restore CA2000
                    newOne.SetParentId(ctx.ActivityContext.TraceId, ctx.ActivityContext.SpanId, ctx.ActivityContext.TraceFlags);
                }

                newOne!.TraceStateString = ctx.ActivityContext.TraceState;

                newOne.SetCustomProperty(CreatedByInstrumentationPropertyName, CreatedByInstrumentationMarker);

                // Starting the new activity make it the Activity.Current one.
                newOne.Start();

                // Set IsAllDataRequested to false for the activity created by the framework to only export the sibling activity and not the framework activity
                activity.IsAllDataRequested = false;
                activity = newOne;
            }

            Baggage.Current = ctx.Baggage;
        }

        // enrich Activity from payload only if sampling decision
        // is favorable.
        if (activity.IsAllDataRequested)
        {
            if (this.options.Filter is { } filter)
            {
                try
                {
                    if (!filter(context))
                    {
                        AspNetCoreInstrumentationEventSource.Log.RequestIsFilteredOut(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName);
                        activity.IsAllDataRequested = false;
                        activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.RequestFilterException(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName, ex);
                    activity.IsAllDataRequested = false;
                    activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                    return;
                }
            }

            if (!Net7OrGreater)
            {
                ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
                ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Server);
            }

            var path = GetRequestPath(request);
            TelemetryHelper.RequestDataHelper.SetActivityDisplayNameAndHttpMethodTag(activity, request.Method);

            // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-spans.md

            if (request.Host.HasValue)
            {
                activity.SetTag(SemanticConventions.AttributeServerAddress, request.Host.Host);

                if (request.Host.Port is { } port)
                {
                    activity.SetTag(SemanticConventions.AttributeServerPort, port);
                }
            }

            if (request.QueryString.HasValue)
            {
                if (this.options.DisableUrlQueryRedaction)
                {
                    activity.SetTag(SemanticConventions.AttributeUrlQuery, request.QueryString.Value);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeUrlQuery, RedactionHelper.GetRedactedQueryString(request.QueryString.Value!));
                }
            }

            activity.SetTag(SemanticConventions.AttributeUrlScheme, request.Scheme);
            activity.SetTag(SemanticConventions.AttributeUrlPath, path);
            activity.SetTag(SemanticConventions.AttributeNetworkProtocolVersion, RequestDataHelper.GetHttpProtocolVersion(request.Protocol));

            if (request.Headers.TryGetValue("User-Agent", out var values))
            {
                var userAgent = values.Count > 0 ? values[0] : null;
                if (!string.IsNullOrEmpty(userAgent))
                {
                    activity.SetTag(SemanticConventions.AttributeUserAgentOriginal, userAgent);
                }
            }

            if (this.options.EnrichWithHttpRequest is { } enricher)
            {
                try
                {
                    enricher(activity, request);
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), nameof(this.OnStartActivity), activity.OperationName, ex);
                }
            }
        }
    }

    public void OnStopActivity(Activity activity, object? payload)
    {
        if (activity.IsAllDataRequested)
        {
            if (payload is not HttpContext context)
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnStopActivity), activity.OperationName);
                return;
            }

            var response = context.Response;

#if !NETSTANDARD
            var routePattern = context.GetHttpRoute();
            if (!string.IsNullOrEmpty(routePattern))
            {
                TelemetryHelper.RequestDataHelper.SetActivityDisplayName(activity, context.Request.Method, routePattern);
                activity.SetTag(SemanticConventions.AttributeHttpRoute, routePattern);
            }
#endif

            activity.SetTag(SemanticConventions.AttributeHttpResponseStatusCode, TelemetryHelper.GetBoxedStatusCode(response.StatusCode));

            if (this.options.EnableGrpcAspNetCoreSupport && IsGrpcRequestProtocol(context.Request.Protocol))
            {
                // Single pass over the tag collection to retrieve both gRPC tags,
                // avoiding two separate GetTagValue iterations.
                string? grpcMethod = null;
                int grpcStatusCode = -1;
                var hasGrpcStatusCode = false;

                var tagEnumerator = activity.EnumerateTagObjects();
                while (tagEnumerator.MoveNext())
                {
                    ref readonly var tag = ref tagEnumerator.Current;
                    if (grpcMethod is null && tag.Key == GrpcTagHelper.GrpcMethodTagName)
                    {
                        grpcMethod = tag.Value as string;
                    }
                    else if (!hasGrpcStatusCode && tag.Key == GrpcTagHelper.GrpcStatusCodeTagName)
                    {
                        hasGrpcStatusCode = int.TryParse(tag.Value as string, NumberStyles.None, CultureInfo.InvariantCulture, out grpcStatusCode);
                    }

                    if (grpcMethod is not null && hasGrpcStatusCode)
                    {
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(grpcMethod))
                {
                    AddGrpcAttributes(activity, grpcMethod!, context, grpcStatusCode, hasGrpcStatusCode);
                }
            }

            if (activity.Status == ActivityStatusCode.Unset)
            {
                activity.SetStatus(SpanHelper.ResolveActivityStatusForHttpStatusCode(activity.Kind, response.StatusCode));
            }

            if (this.options.EnrichWithHttpResponse is { } enricher)
            {
                try
                {
                    enricher(activity, response);
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), nameof(this.OnStopActivity), activity.OperationName, ex);
                }
            }
        }

        if (ReferenceEquals(activity.GetCustomProperty(CreatedByInstrumentationPropertyName), CreatedByInstrumentationMarker))
        {
            // If instrumentation started a new Activity, it must
            // be stopped here.
            activity.Stop();

            // After the activity.Stop() code, Activity.Current becomes null.
            // If Asp.Net Core uses Activity.Current?.Stop() - it'll not stop the activity
            // it created.
            // Currently Asp.Net core does not use Activity.Current, instead it stores a
            // reference to its activity, and calls .Stop on it.

            // TODO: Should we still restore Activity.Current here?
            // If yes, then we need to store the asp.net core activity inside
            // the one created by the instrumentation.
            // And retrieve it here, and set it to Current.
        }
    }

    public void OnException(Activity activity, object? payload)
    {
        if (activity.IsAllDataRequested)
        {
            // We need to use reflection here as the payload type is not a defined public type.
            if (!TryFetchException(payload, out var exc))
            {
                AspNetCoreInstrumentationEventSource.Log.NullPayload(nameof(HttpInListener), nameof(this.OnException), activity.OperationName);
                return;
            }

            activity.SetTag(SemanticConventions.AttributeErrorType, exc.GetType().FullName);

            if (this.options.RecordException)
            {
                activity.AddException(exc);
            }

            activity.SetStatus(ActivityStatusCode.Error);

            if (this.options.EnrichWithException is { } enricher)
            {
                try
                {
                    enricher(activity, exc);
                }
                catch (Exception ex)
                {
                    AspNetCoreInstrumentationEventSource.Log.EnrichmentException(nameof(HttpInListener), nameof(this.OnException), activity.OperationName, ex);
                }
            }
        }

        // See https://github.com/dotnet/aspnetcore/blob/690d78279e940d267669f825aa6627b0d731f64c/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L252
        // and https://github.com/dotnet/aspnetcore/blob/690d78279e940d267669f825aa6627b0d731f64c/src/Middleware/Diagnostics/src/DeveloperExceptionPage/DeveloperExceptionPageMiddlewareImpl.cs#L174
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top level properties are preserved")]
#endif
        static bool TryFetchException(object? payload, [NotNullWhen(true)] out Exception? exc)
        {
            return ExceptionPropertyFetcher.TryFetch(payload, out exc) && exc != null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddGrpcAttributes(Activity activity, string grpcMethod, HttpContext context, int grpcStatusCode, bool validStatusCode)
    {
        var details = GrpcMethodCache.Get(grpcMethod);

        // The RPC semantic conventions indicate the span name
        // should not have a leading forward slash.
        // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/rpc/rpc-spans.md#span-name
        activity.DisplayName = details.DisplayName;

        activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

        // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/rpc/rpc-spans.md

        if (context.Connection.RemoteIpAddress != null)
        {
            activity.SetTag(SemanticConventions.AttributeClientAddress, context.Connection.RemoteIpAddress.ToString());
        }

        activity.SetTag(SemanticConventions.AttributeClientPort, context.Connection.RemotePort);

        if (validStatusCode)
        {
            activity.SetStatus(GrpcTagHelper.ResolveSpanStatusForGrpcStatusCodeOnServer(grpcStatusCode));
        }

        if (details.IsParsed)
        {
            activity.SetTag(SemanticConventions.AttributeRpcService, details.RpcService);
            activity.SetTag(SemanticConventions.AttributeRpcMethod, details.RpcMethod);

            // Remove the grpc.method tag added by the gRPC .NET library
            activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);

            // Remove the grpc.status_code tag added by the gRPC .NET library
            activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, null);

            if (validStatusCode)
            {
                // setting rpc.grpc.status_code
                activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, grpcStatusCode);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetRequestPath(HttpRequest request) => !request.PathBase.HasValue
        ? request.Path.Value ?? "/"
        : !request.Path.HasValue ? request.PathBase.Value : string.Concat(request.PathBase.Value, request.Path.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsGrpcRequestProtocol(string protocol) =>
        protocol == "HTTP/2" || protocol == "HTTP/3";

    private readonly struct GrpcMethodDetails
    {
        public GrpcMethodDetails(string displayName, string? rpcService, string? rpcMethod, bool isParsed)
        {
            this.DisplayName = displayName;
            this.RpcService = rpcService;
            this.RpcMethod = rpcMethod;
            this.IsParsed = isParsed;
        }

        public readonly string DisplayName { get; }

        public readonly string? RpcService { get; }

        public readonly string? RpcMethod { get; }

        public readonly bool IsParsed { get; }
    }

    private sealed class GrpcMethodDetailsCache
    {
        private const int MaxCacheSize = 512;
        private readonly ConcurrentDictionary<string, GrpcMethodDetails> cache = new();

        public GrpcMethodDetails Get(string grpcMethod)
        {
            if (this.cache.TryGetValue(grpcMethod, out var details))
            {
                return details;
            }

            // If the cache has reached its maximum size, just create a value without caching
            return this.cache.Count >= MaxCacheSize ? Create(grpcMethod) : this.cache.GetOrAdd(grpcMethod, Create);
        }

        private static GrpcMethodDetails Create(string method)
        {
            var displayName = method.Length > 0 && method[0] == '/' ? method.Substring(1) : method;
            var isParsed = GrpcTagHelper.TryParseRpcServiceAndRpcMethod(method, out var serviceName, out var methodName);

            return new(displayName, isParsed ? serviceName : null, isParsed ? methodName : null, isParsed);
        }
    }
}
