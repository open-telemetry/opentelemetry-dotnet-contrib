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

        try
        {
            if (WcfInstrumentationActivitySource.Options == null || WcfInstrumentationActivitySource.Options.IncomingRequestFilter?.Invoke(request) == false)
            {
                WcfInstrumentationEventSource.Log.RequestIsFilteredOut();
                return null;
            }
        }
        catch (Exception ex)
        {
            WcfInstrumentationEventSource.Log.RequestFilterException(ex);
            return null;
        }

        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        var ctx = textMapPropagator.Extract(default, request, WcfInstrumentationActivitySource.MessageHeaderValuesGetter);

        Activity? activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
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
                activity.SetTag(WcfInstrumentationConstants.RpcSystemTag, WcfInstrumentationConstants.WcfSystemValue);

                if (!this.actionMappings.TryGetValue(action, out ActionMetadata actionMetadata))
                {
                    actionMetadata = new ActionMetadata(
                        contractName: null,
                        operationName: action);
                }

                activity.SetTag(WcfInstrumentationConstants.RpcServiceTag, actionMetadata.ContractName);
                activity.SetTag(WcfInstrumentationConstants.RpcMethodTag, actionMetadata.OperationName);

                if (WcfInstrumentationActivitySource.Options.SetSoapMessageVersion)
                {
                    activity.SetTag(WcfInstrumentationConstants.SoapMessageVersionTag, request.Version.ToString());
                }

                var localAddressUri = channel.LocalAddress?.Uri;
                if (localAddressUri != null)
                {
                    activity.SetTag(WcfInstrumentationConstants.NetHostNameTag, localAddressUri.Host);
                    activity.SetTag(WcfInstrumentationConstants.NetHostPortTag, localAddressUri.Port);
                    activity.SetTag(WcfInstrumentationConstants.WcfChannelSchemeTag, localAddressUri.Scheme);
                    activity.SetTag(WcfInstrumentationConstants.WcfChannelPathTag, localAddressUri.LocalPath);
                }

                try
                {
                    WcfInstrumentationActivitySource.Options.Enrich?.Invoke(activity, WcfEnrichEventNames.AfterReceiveRequest, request);
                }
                catch (Exception ex)
                {
                    WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            if (!(textMapPropagator is TraceContextPropagator))
            {
                Baggage.Current = ctx.Baggage;
            }
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
                if (reply.IsFault)
                {
                    activity.SetStatus(Status.Error);
                }

                activity.SetTag(WcfInstrumentationConstants.SoapReplyActionTag, reply.Headers.Action);
                try
                {
                    WcfInstrumentationActivitySource.Options!.Enrich?.Invoke(activity, WcfEnrichEventNames.BeforeSendReply, reply);
                }
                catch (Exception ex)
                {
                    WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }

            activity.Stop();

            if (!(Propagators.DefaultTextMapPropagator is TraceContextPropagator))
            {
                Baggage.Current = default;
            }
        }
    }
}

#endif
