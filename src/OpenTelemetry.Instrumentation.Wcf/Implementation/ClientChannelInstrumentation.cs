// <copyright file="ClientChannelInstrumentation.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.ServiceModel.Channels;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf.Implementation;

internal static class ClientChannelInstrumentation
{
    public static RequestTelemetryState BeforeSendRequest(Message request, Uri remoteChannelAddress)
    {
        if (!ShouldInstrumentRequest(request))
        {
            return new RequestTelemetryState { SuppressionScope = SuppressDownstreamInstrumentation() };
        }

        Activity activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
            WcfInstrumentationActivitySource.OutgoingRequestActivityName,
            ActivityKind.Client);
        IDisposable suppressionScope = SuppressDownstreamInstrumentation();

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
                activity.SetTag(WcfInstrumentationConstants.RpcSystemTag, WcfInstrumentationConstants.WcfSystemValue);

                var actionMetadata = GetActionMetadata(request, action);
                activity.SetTag(WcfInstrumentationConstants.RpcServiceTag, actionMetadata.ContractName);
                activity.SetTag(WcfInstrumentationConstants.RpcMethodTag, actionMetadata.OperationName);

                if (WcfInstrumentationActivitySource.Options.SetSoapMessageVersion)
                {
                    activity.SetTag(WcfInstrumentationConstants.SoapMessageVersionTag, request.Version.ToString());
                }

                var remoteAddressUri = request.Headers.To ?? remoteChannelAddress;
                if (remoteAddressUri != null)
                {
                    activity.SetTag(WcfInstrumentationConstants.NetPeerNameTag, remoteAddressUri.Host);
                    activity.SetTag(WcfInstrumentationConstants.NetPeerPortTag, remoteAddressUri.Port);
                    activity.SetTag(WcfInstrumentationConstants.WcfChannelSchemeTag, remoteAddressUri.Scheme);
                    activity.SetTag(WcfInstrumentationConstants.WcfChannelPathTag, remoteAddressUri.LocalPath);
                }

                if (request.Properties.Via != null)
                {
                    activity.SetTag(WcfInstrumentationConstants.SoapViaTag, request.Properties.Via.ToString());
                }

                try
                {
                    WcfInstrumentationActivitySource.Options.Enrich?.Invoke(activity, WcfEnrichEventNames.BeforeSendRequest, request);
                }
                catch (Exception ex)
                {
                    WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        return new RequestTelemetryState
        {
            SuppressionScope = suppressionScope,
            Activity = activity,
        };
    }

    public static void AfterRequestCompleted(Message reply, RequestTelemetryState state)
    {
        Guard.ThrowIfNull(state);

        state.SuppressionScope?.Dispose();

        if (state.Activity is Activity activity)
        {
            if (activity.IsAllDataRequested)
            {
                if (reply == null || reply.IsFault)
                {
                    activity.SetStatus(Status.Error);
                }

                if (reply != null)
                {
                    activity.SetTag(WcfInstrumentationConstants.SoapReplyActionTag, reply.Headers.Action);
                    try
                    {
                        WcfInstrumentationActivitySource.Options.Enrich?.Invoke(activity, WcfEnrichEventNames.AfterReceiveReply, reply);
                    }
                    catch (Exception ex)
                    {
                        WcfInstrumentationEventSource.Log.EnrichmentException(ex);
                    }
                }
            }

            activity.Stop();
        }
    }

    private static IDisposable SuppressDownstreamInstrumentation()
    {
        return WcfInstrumentationActivitySource.Options?.SuppressDownstreamInstrumentation ?? false
            ? SuppressInstrumentationScope.Begin()
            : null;
    }

    private static ActionMetadata GetActionMetadata(Message request, string action)
    {
        ActionMetadata actionMetadata = null;
        if (request.Properties.TryGetValue(TelemetryContextMessageProperty.Name, out object telemetryContextProperty))
        {
            var actionMappings = (telemetryContextProperty as TelemetryContextMessageProperty)?.ActionMappings;
            if (actionMappings != null && actionMappings.TryGetValue(action, out ActionMetadata metadata))
            {
                actionMetadata = metadata;
            }
        }

        return actionMetadata != null ? actionMetadata : new ActionMetadata
        {
            ContractName = null,
            OperationName = action,
        };
    }

    private static bool ShouldInstrumentRequest(Message request)
    {
        try
        {
            if (WcfInstrumentationActivitySource.Options == null || WcfInstrumentationActivitySource.Options.OutgoingRequestFilter?.Invoke(request) == false)
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
