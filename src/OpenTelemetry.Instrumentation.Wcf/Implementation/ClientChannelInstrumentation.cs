// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal static class ClientChannelInstrumentation
{
    public static RequestTelemetryState BeforeSendRequest(Message request, Uri? remoteChannelAddress)
    {
        if (!ShouldInstrumentRequest(request, out var options))
        {
            return new() { SuppressionScope = SuppressDownstreamInstrumentation(options) };
        }

        var activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
            WcfInstrumentationActivitySource.OutgoingRequestActivityName,
            ActivityKind.Client);

        var suppressionScope = SuppressDownstreamInstrumentation(options);

        if (activity != null)
        {
            var action = string.Empty;
            if (!string.IsNullOrEmpty(request.Headers.Action))
            {
                action = request.Headers.Action;
                activity.DisplayName = action;
            }

            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                request,
                WcfInstrumentationActivitySource.MessageHeaderValueSetter);

            if (activity.IsAllDataRequested)
            {
                var actionMetadata = GetActionMetadata(request, action);

                activity.SetTag(SemanticConventions.AttributeRpcMethod, actionMetadata.OperationName);

                if (options?.EmitOldRpcAttributes == true)
                {
                    activity.SetTag(SemanticConventions.AttributeRpcSystem, WcfInstrumentationConstants.WcfSystemValue);
                    activity.SetTag(SemanticConventions.AttributeRpcService, actionMetadata.ContractName);
                }

                if (options?.EmitNewRpcAttributes == true)
                {
                    activity.SetTag(SemanticConventions.AttributeRpcSystemName, WcfInstrumentationConstants.WcfSystemValue);
                }

                var remoteAddressUri = request.Headers.To ?? remoteChannelAddress;
                if (remoteAddressUri != null)
                {
                    if (options?.EmitOldRpcAttributes is true)
                    {
                        activity.SetTag(SemanticConventions.AttributeNetPeerName, remoteAddressUri.Host);
                        activity.SetTag(SemanticConventions.AttributeNetPeerPort, remoteAddressUri.Port);
                    }

                    if (options?.EmitNewRpcAttributes is true)
                    {
                        activity.SetTag(SemanticConventions.AttributeServerAddress, remoteAddressUri.Host);
                        activity.SetTag(SemanticConventions.AttributeServerPort, remoteAddressUri.Port);

                        if (remoteAddressUri.Host is { Length: > 0 } host)
                        {
                            var uriHostNameType = Uri.CheckHostName(host);

                            if (uriHostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
                            {
                                activity.SetTag(SemanticConventions.AttributeNetworkPeerAddress, host);
                                activity.SetTag(SemanticConventions.AttributeNetworkPeerPort, remoteAddressUri.Port);
                            }
                        }
                    }

                    // TODO Should we continue to emit WcfInstrumentationConstants given they aren't in the Semantic Conventions?
                    // See https://github.com/open-telemetry/semantic-conventions/issues/2741
                    activity.SetTag(WcfInstrumentationConstants.AttributeWcfChannelScheme, remoteAddressUri.Scheme);
                    activity.SetTag(WcfInstrumentationConstants.AttributeWcfChannelPath, remoteAddressUri.LocalPath);
                }

                // TODO Should we continue to emit WcfInstrumentationConstants given they aren't in the Semantic Conventions?
                // See https://github.com/open-telemetry/semantic-conventions/issues/2741
                if (options?.SetSoapMessageVersion == true)
                {
                    activity.SetTag(WcfInstrumentationConstants.AttributeSoapMessageVersion, request.Version.ToString());
                }

                if (request.Properties.Via != null)
                {
                    activity.SetTag(WcfInstrumentationConstants.AttributeSoapVia, request.Properties.Via.ToString());
                }

                if (options?.Enrich is { } enrich)
                {
                    try
                    {
                        enrich(activity, WcfEnrichEventNames.BeforeSendRequest, request);
                    }
                    catch (Exception ex)
                    {
                        WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }
            }
        }

        return new RequestTelemetryState
        {
            SuppressionScope = suppressionScope,
            Activity = activity,
        };
    }

    public static void AfterRequestCompleted(Message? reply, RequestTelemetryState? state, Exception? exception = null)
    {
        Guard.ThrowIfNull(state);
        state.SuppressionScope?.Dispose();

        if (state.Activity is Activity activity)
        {
            var options = WcfInstrumentationActivitySource.Options;

            if (activity.IsAllDataRequested)
            {
                if (exception is FaultException fault && options?.EmitNewRpcAttributes is true)
                {
                    activity.SetTag(SemanticConventions.AttributeRpcResponseStatusCode, fault.Code);
                }

                if (reply == null || reply.IsFault)
                {
                    activity.SetStatus(ActivityStatusCode.Error);

                    if (options?.RecordException == true && exception != null)
                    {
                        activity.AddException(exception);
                    }
                }

                if (reply != null)
                {
                    activity.SetTag(WcfInstrumentationConstants.AttributeSoapReplyAction, reply.Headers.Action);

                    if (options?.Enrich is { } enrich)
                    {
                        try
                        {
                            enrich(activity, WcfEnrichEventNames.AfterReceiveReply, reply);
                        }
                        catch (Exception ex)
                        {
                            WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                        }
                    }
                }
            }

            activity.Stop();
        }
    }

    private static IDisposable? SuppressDownstreamInstrumentation(WcfInstrumentationOptions? options) =>
        options?.SuppressDownstreamInstrumentation == true
            ? SuppressInstrumentationScope.Begin()
            : null;

    private static ActionMetadata GetActionMetadata(Message request, string action)
    {
        ActionMetadata? actionMetadata = null;
        if (request.Properties.TryGetValue(TelemetryContextMessageProperty.Name, out var telemetryContextProperty))
        {
            var actionMappings = (telemetryContextProperty as TelemetryContextMessageProperty)?.ActionMappings;
            if (actionMappings != null && actionMappings.TryGetValue(action, out var metadata))
            {
                actionMetadata = metadata;
            }
        }

        return actionMetadata ?? new ActionMetadata(
            contractName: null,
            operationName: action);
    }

    private static bool ShouldInstrumentRequest(Message request, out WcfInstrumentationOptions? options)
    {
        options = WcfInstrumentationActivitySource.Options;

        try
        {
            if (options == null || options.OutgoingRequestFilter?.Invoke(request) == false)
            {
                WcfInstrumentationEventSource.Log.RequestIsFilteredOut();
                return false;
            }
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.RequestFilterException(ex);
            return false;
        }

        return true;
    }
}
