// <copyright file="TelemetryClientMessageInspector.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Wcf
{
    /// <summary>
    /// An <see cref="IClientMessageInspector"/> implementation which adds telemetry to outgoing requests.
    /// </summary>
    public class TelemetryClientMessageInspector : IClientMessageInspector
    {
        private readonly IDictionary<string, ActionMetadata> actionMappings;

        internal TelemetryClientMessageInspector(IDictionary<string, ActionMetadata> actionMappings)
        {
            this.actionMappings = actionMappings ?? throw new ArgumentNullException(nameof(actionMappings));
        }

        /// <inheritdoc/>
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            try
            {
                if (WcfInstrumentationActivitySource.Options == null || WcfInstrumentationActivitySource.Options.OutgoingRequestFilter?.Invoke(request) == false)
                {
                    WcfInstrumentationEventSource.Log.RequestIsFilteredOut();
                    return new State
                    {
                        SuppressionScope = this.SuppressDownstreamInstrumentation(),
                    };
                }
            }
            catch (Exception ex)
            {
                WcfInstrumentationEventSource.Log.RequestFilterException(ex);
                return new State
                {
                    SuppressionScope = this.SuppressDownstreamInstrumentation(),
                };
            }

            Activity activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
                WcfInstrumentationActivitySource.OutgoingRequestActivityName,
                ActivityKind.Client);
            IDisposable suppressionScope = this.SuppressDownstreamInstrumentation();

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

                Propagators.DefaultTextMapPropagator.Inject(
                    new PropagationContext(activity.Context, Baggage.Current),
                    request,
                    WcfInstrumentationActivitySource.MessageHeaderValueSetter);

                if (activity.IsAllDataRequested)
                {
                    activity.SetTag(WcfInstrumentationConstants.RpcSystemTag, WcfInstrumentationConstants.WcfSystemValue);

                    if (!this.actionMappings.TryGetValue(action, out ActionMetadata actionMetadata))
                    {
                        actionMetadata = new ActionMetadata
                        {
                            ContractName = null,
                            OperationName = action,
                        };
                    }

                    activity.SetTag(WcfInstrumentationConstants.RpcServiceTag, actionMetadata.ContractName);
                    activity.SetTag(WcfInstrumentationConstants.RpcMethodTag, actionMetadata.OperationName);

                    if (WcfInstrumentationActivitySource.Options.SetSoapMessageVersion)
                    {
                        activity.SetTag(WcfInstrumentationConstants.SoapMessageVersionTag, request.Version.ToString());
                    }

                    var remoteAddressUri = request.Headers.To ?? channel.RemoteAddress?.Uri;
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

            return new State
            {
                SuppressionScope = suppressionScope,
                Activity = activity,
            };
        }

        /// <inheritdoc/>
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            State state = (State)correlationState;

            state.SuppressionScope?.Dispose();

            if (state.Activity is Activity activity)
            {
                if (activity.IsAllDataRequested)
                {
                    if (reply.IsFault)
                    {
                        activity.SetStatus(Status.Error);
                    }

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

                activity.Stop();
            }
        }

        private IDisposable SuppressDownstreamInstrumentation()
        {
            return WcfInstrumentationActivitySource.Options?.SuppressDownstreamInstrumentation ?? false
                ? SuppressInstrumentationScope.Begin()
                : null;
        }

        private class State
        {
            public IDisposable SuppressionScope;
            public Activity Activity;
        }
    }
}
