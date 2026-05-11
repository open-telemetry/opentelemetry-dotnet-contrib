// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

/// <summary>
/// An <see cref="IDispatchMessageInspector"/> implementation which adds telemetry to incoming requests.
/// </summary>
internal class TelemetryDispatchMessageInspector : IDispatchMessageInspector
{
    private readonly IDictionary<string, ActionMetadata> actionMappings;

    internal TelemetryDispatchMessageInspector(IDictionary<string, ActionMetadata> actionMappings)
    {
        Guard.ThrowIfNull(actionMappings);

        this.actionMappings = actionMappings;
    }

    /// <inheritdoc/>
    public object? AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
    {
        Guard.ThrowIfNull(request);
        Guard.ThrowIfNull(channel);

        WcfInstrumentationOptions options;

        try
        {
            if (WcfInstrumentationActivitySource.Options is not { } wcfOptions ||
                wcfOptions.IncomingRequestFilter?.Invoke(request) == false)
            {
                WcfInstrumentationEventSource.Log.RequestIsFilteredOut();
                return null;
            }

            options = wcfOptions;
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.RequestFilterException(ex);
            return null;
        }

        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        var ctx = textMapPropagator.Extract(default, request, WcfInstrumentationActivitySource.MessageHeaderValuesGetter);

        var activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
            WcfInstrumentationActivitySource.IncomingRequestActivityName,
            ActivityKind.Server,
            ctx.ActivityContext);

        if (activity != null)
        {
            string action;
            if (!string.IsNullOrEmpty(request.Headers.Action))
            {
                action = request.Headers.Action;
                activity.DisplayName = action;
            }
            else
            {
                action = string.Empty;
            }

            if (activity.IsAllDataRequested)
            {
                if (!this.actionMappings.TryGetValue(action, out var actionMetadata))
                {
                    actionMetadata = new ActionMetadata(
                        contractName: null,
                        operationName: action);
                }

                activity.SetTag(SemanticConventions.AttributeRpcMethod, actionMetadata.OperationName);

                if (options.EmitOldRpcAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeRpcSystem, WcfInstrumentationConstants.WcfSystemValue);
                    activity.SetTag(SemanticConventions.AttributeRpcService, actionMetadata.ContractName);
                }

                if (options.EmitNewRpcAttributes)
                {
                    activity.SetTag(SemanticConventions.AttributeRpcSystemName, WcfInstrumentationConstants.WcfSystemValue);
                }

                var localAddressUri = channel.LocalAddress?.Uri;
                if (localAddressUri != null)
                {
                    if (options?.EmitOldRpcAttributes is true)
                    {
                        activity.SetTag(SemanticConventions.AttributeNetHostName, localAddressUri.Host);
                        activity.SetTag(SemanticConventions.AttributeNetHostPort, localAddressUri.Port);
                    }

                    if (options?.EmitNewRpcAttributes is true)
                    {
                        activity.SetTag(SemanticConventions.AttributeServerAddress, localAddressUri.Host);
                        activity.SetTag(SemanticConventions.AttributeServerPort, localAddressUri.Port);

                        if (localAddressUri.Host is { Length: > 0 } host)
                        {
                            var uriHostNameType = Uri.CheckHostName(host);

                            if (uriHostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
                            {
                                activity.SetTag(SemanticConventions.AttributeNetworkPeerAddress, host);
                                activity.SetTag(SemanticConventions.AttributeNetworkPeerPort, localAddressUri.Port);
                            }
                        }
                    }

                    activity.SetTag(WcfInstrumentationConstants.AttributeWcfChannelScheme, localAddressUri.Scheme);
                    activity.SetTag(WcfInstrumentationConstants.AttributeWcfChannelPath, localAddressUri.LocalPath);
                }

                if (options?.SetSoapMessageVersion == true)
                {
                    activity.SetTag(WcfInstrumentationConstants.AttributeSoapMessageVersion, request.Version.ToString());
                }

                if (options?.Enrich is { } enrich)
                {
                    try
                    {
                        enrich(activity, WcfEnrichEventNames.AfterReceiveRequest, request);
                    }
                    catch (Exception ex)
                    {
                        WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }
            }

            if (textMapPropagator is not TraceContextPropagator)
            {
                Baggage.Current = ctx.Baggage;
            }
        }

        if (options?.RecordException == true)
        {
            OperationContext.Current?.Extensions.Add(new WcfOperationContext(activity));
        }

        return activity;
    }

    /// <inheritdoc/>
    public void BeforeSendReply(ref Message reply, object? correlationState)
    {
        if (correlationState is Activity activity)
        {
            if (activity.IsAllDataRequested && reply != null)
            {
                var options = WcfInstrumentationActivitySource.Options;

                if (reply.IsFault)
                {
                    activity.SetStatus(ActivityStatusCode.Error);

                    if (options?.EmitNewRpcAttributes == true &&
                        OperationContext.Current?.IncomingMessageProperties[HttpResponseMessageProperty.Name] is HttpResponseMessageProperty response)
                    {
                        activity.SetTag(SemanticConventions.AttributeRpcResponseStatusCode, response.StatusCode);
                    }
                }

                activity.SetTag(WcfInstrumentationConstants.AttributeSoapReplyAction, reply.Headers.Action);

                if (options?.Enrich is { } enrich)
                {
                    try
                    {
                        enrich(activity, WcfEnrichEventNames.BeforeSendReply, reply);
                    }
                    catch (Exception ex)
                    {
                        WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }
            }

            activity.Stop();

            if (Propagators.DefaultTextMapPropagator is not TraceContextPropagator)
            {
                Baggage.Current = default;
            }
        }
    }
}

#endif
