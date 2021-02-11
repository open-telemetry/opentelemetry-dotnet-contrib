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
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Contrib.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf
{
    /// <summary>
    /// An <see cref="IClientMessageInspector"/> implementation which adds telemetry to outgoing requests.
    /// </summary>
    public class TelemetryClientMessageInspector : IClientMessageInspector
    {
        /// <inheritdoc/>
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            try
            {
                if (WcfInstrumentationActivitySource.Options == null || WcfInstrumentationActivitySource.Options.OutgoingRequestFilter?.Invoke(request) == false)
                {
                    WcfInstrumentationEventSource.Log.RequestIsFilteredOut(request.Headers.Action);
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
                string action = request.Headers.Action;
                activity.DisplayName = action;

                WcfInstrumentationActivitySource.Options.Propagator.Inject(
                    new PropagationContext(activity.Context, Baggage.Current),
                    request,
                    WcfInstrumentationActivitySource.MessageHeaderValueSetter);

                if (activity.IsAllDataRequested)
                {
                    int lastIndex = action.LastIndexOf('/');

                    activity.SetTag("rpc.system", "wcf");
                    activity.SetTag("rpc.service", action.Substring(0, lastIndex));
                    activity.SetTag("rpc.method", action.Substring(lastIndex + 1));
                    activity.SetTag("net.peer.name", channel.RemoteAddress.Uri.Host);
                    activity.SetTag("net.peer.port", channel.RemoteAddress.Uri.Port);

                    activity.SetTag("soap.version", request.Version.ToString());
                    activity.SetTag("wcf.channel.scheme", channel.RemoteAddress.Uri.Scheme);
                    activity.SetTag("wcf.channel.path", channel.RemoteAddress.Uri.LocalPath);
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

            Activity activity = state.Activity;
            if (activity != null)
            {
                if (activity.IsAllDataRequested)
                {
                    if (reply.IsFault)
                    {
                        activity.SetStatus(Status.Error);
                    }

                    activity.SetTag("soap.reply_action", reply.Headers.Action);
                }

                activity.Stop();
            }

            state.SuppressionScope?.Dispose();
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
