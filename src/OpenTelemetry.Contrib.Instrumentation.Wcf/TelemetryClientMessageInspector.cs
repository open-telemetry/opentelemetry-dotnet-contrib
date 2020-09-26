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

using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using OpenTelemetry.Context.Propagation;
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
                    // AspNetInstrumentationEventSource.Log.RequestIsFilteredOut(activity.OperationName);
                    return null;
                }
            }
            catch
            {
                /*(Exception ex)*/
                // AspNetInstrumentationEventSource.Log.RequestFilterException(ex);
                return null;
            }

            Activity activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
                WcfInstrumentationActivitySource.OutgoingRequestActivityName,
                ActivityKind.Client);

            if (activity != null)
            {
                if (WcfInstrumentationActivitySource.Options.SuppressDownstreamInstrumentation)
                {
                    SuppressInstrumentationScope.Enter();
                }

                activity.DisplayName = request.Headers.Action;

                WcfInstrumentationActivitySource.Options.Propagator.Inject(
                    new PropagationContext(activity.Context, Baggage.Current),
                    request,
                    WcfInstrumentationActivitySource.MessageHeaderValueSetter);

                if (activity.IsAllDataRequested)
                {
                    activity.SetTag("soap.version", request.Version.ToString());
                    activity.SetTag("wcf.channel.scheme", $"{channel.RemoteAddress.Uri.Scheme}");
                    activity.SetTag("wcf.channel.host", $"{channel.RemoteAddress.Uri.Host}:{channel.RemoteAddress.Uri.Port}");
                    activity.SetTag("wcf.channel.path", channel.RemoteAddress.Uri.LocalPath);
                }
            }

            return activity;
        }

        /// <inheritdoc/>
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            Activity activity = (Activity)correlationState;
            if (activity != null)
            {
                if (activity.IsAllDataRequested)
                {
                    activity.SetStatus(!reply.IsFault ? Status.Ok : Status.Unknown);
                    activity.SetTag("soap.reply_action", reply.Headers.Action);
                }

                activity.Stop();
            }
        }
    }
}
