// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;

internal sealed class GrpcClientDiagnosticListener : ListenerHandler
{
    internal static readonly Assembly Assembly = typeof(GrpcClientDiagnosticListener).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name!;
    internal static readonly string Version = Assembly.GetPackageVersion();
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    private const string OnStartEvent = "Grpc.Net.Client.GrpcOut.Start";
    private const string OnStopEvent = "Grpc.Net.Client.GrpcOut.Stop";

    private static readonly PropertyFetcher<HttpRequestMessage> StartRequestFetcher = new("Request");
    private static readonly PropertyFetcher<HttpResponseMessage> StopResponseFetcher = new("Response");

    private readonly GrpcClientTraceInstrumentationOptions options;

    public GrpcClientDiagnosticListener(GrpcClientTraceInstrumentationOptions options)
        : base("Grpc.Net.Client")
    {
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
            default:
                break;
        }
    }

    public void OnStartActivity(Activity activity, object? payload)
    {
        // The overall flow of what GrpcClient library does is as below:
        // Activity.Start()
        // DiagnosticSource.WriteEvent("Start", payload)
        // DiagnosticSource.WriteEvent("Stop", payload)
        // Activity.Stop()

        // This method is in the WriteEvent("Start", payload) path.
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (Sdk.SuppressInstrumentation)
        {
            return;
        }

        // Ensure context propagation irrespective of sampling decision
        if (!TryFetchRequest(payload, out var request))
        {
            GrpcInstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener), nameof(this.OnStartActivity));
            return;
        }

        if (this.options.SuppressDownstreamInstrumentation)
        {
            SuppressInstrumentationScope.Enter();

            // If we are suppressing downstream instrumentation then inject
            // context here. Grpc.Net.Client uses HttpClient, so
            // SuppressDownstreamInstrumentation means that the
            // OpenTelemetry instrumentation for HttpClient will not be
            // invoked.

            // Note that HttpClient natively generates its own activity and
            // propagates W3C trace context headers regardless of whether
            // OpenTelemetry HttpClient instrumentation is invoked.
            // Therefore, injecting here preserves more intuitive span
            // parenting - i.e., the entry point span of a downstream
            // service would be parented to the span generated by
            // Grpc.Net.Client rather than the span generated natively by
            // HttpClient. Injecting here also ensures that baggage is
            // propagated to downstream services.
            // Injecting context here also ensures that the configured
            // propagator is used, as HttpClient by itself will only
            // do TraceContext propagation.
            var textMapPropagator = Propagators.DefaultTextMapPropagator;
            textMapPropagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                request,
                HttpRequestMessageContextPropagation.HeaderValueSetter);
        }

        if (activity.IsAllDataRequested)
        {
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
            ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);

            var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            if (grpcMethod != null)
            {
                activity.DisplayName = grpcMethod.Trim('/');

                if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
                {
                    activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                    activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);

                    // Remove the grpc.method tag added by the gRPC .NET library
                    activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);
                }
            }

            activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

            var requestUri = request.RequestUri;

            if (requestUri != null)
            {
                var uriHostNameType = Uri.CheckHostName(requestUri.Host);

                if (uriHostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
                {
                    activity.SetTag(SemanticConventions.AttributeServerSocketAddress, requestUri.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeServerAddress, requestUri.Host);
                }

                activity.SetTag(SemanticConventions.AttributeServerPort, requestUri.Port);
            }

            try
            {
                this.options.EnrichWithHttpRequestMessage?.Invoke(activity, request);
            }
            catch (Exception ex)
            {
                GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }

        // See https://github.com/grpc/grpc-dotnet/blob/ff1a07b90c498f259e6d9f4a50cdad7c89ecd3c0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L1180-L1183
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top level properties are preserved")]
#endif
        static bool TryFetchRequest(object? payload, [NotNullWhen(true)] out HttpRequestMessage? request)
            => StartRequestFetcher.TryFetch(payload, out request) && request != null;
    }

    public void OnStopActivity(Activity activity, object? payload)
    {
        if (activity.IsAllDataRequested)
        {
            var validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out var status);
            if (validConversion)
            {
                if (activity.Status == ActivityStatusCode.Unset)
                {
                    activity.SetStatus(GrpcTagHelper.ResolveSpanStatusForGrpcStatusCode(status));
                }

                // setting rpc.grpc.status_code
                activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);
            }

            // Remove the grpc.status_code tag added by the gRPC .NET library
            activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, null);

            if (TryFetchResponse(payload, out var response))
            {
                try
                {
                    this.options.EnrichWithHttpResponseMessage?.Invoke(activity, response);
                }
                catch (Exception ex)
                {
                    GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        // See https://github.com/grpc/grpc-dotnet/blob/ff1a07b90c498f259e6d9f4a50cdad7c89ecd3c0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L1180-L1183
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top level properties are preserved")]
#endif
        static bool TryFetchResponse(object? payload, [NotNullWhen(true)] out HttpResponseMessage? response)
            => StopResponseFetcher.TryFetch(payload, out response) && response != null;
    }
}
