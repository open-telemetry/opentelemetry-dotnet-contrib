// <copyright file="TelemetryDispatchMessageInspector.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Contrib.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Wcf
{
    /// <summary>
    /// An <see cref="IDispatchMessageInspector"/> implementation which adds telemetry to incoming requests.
    /// </summary>
    public class TelemetryDispatchMessageInspector : IDispatchMessageInspector
    {
        /// <inheritdoc/>
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            try
            {
                if (WcfInstrumentationActivitySource.Options == null || WcfInstrumentationActivitySource.Options.IncomingRequestFilter?.Invoke(request) == false)
                {
                    WcfInstrumentationEventSource.Log.RequestIsFilteredOut(request.Headers.Action);
                    return null;
                }
            }
            catch (Exception ex)
            {
                WcfInstrumentationEventSource.Log.RequestFilterException(ex);
                return null;
            }

            var ctx = WcfInstrumentationActivitySource.Options.Propagator.Extract(default, request, WcfInstrumentationActivitySource.MessageHeaderValuesGetter);

            Activity activity = WcfInstrumentationActivitySource.ActivitySource.StartActivity(
                WcfInstrumentationActivitySource.IncomingRequestActivityName,
                ActivityKind.Server,
                ctx.ActivityContext);

            if (activity != null)
            {
                if (WcfInstrumentationActivitySource.Options.SuppressDownstreamInstrumentation)
                {
                    SuppressInstrumentationScope.Enter();
                }

                activity.DisplayName = request.Headers.Action;

                if (activity.IsAllDataRequested)
                {
                    activity.SetTag("soap.version", request.Version.ToString());
                    activity.SetTag("wcf.channel.scheme", $"{channel.LocalAddress.Uri.Scheme}");
                    activity.SetTag("wcf.channel.host", $"{channel.LocalAddress.Uri.Host}:{channel.LocalAddress.Uri.Port}");
                    activity.SetTag("wcf.channel.path", channel.LocalAddress.Uri.LocalPath);
                }
            }

            if (ctx.Baggage != default)
            {
                Baggage.Current = ctx.Baggage;
            }

            return activity;
        }

        /// <inheritdoc/>
        public void BeforeSendReply(ref Message reply, object correlationState)
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
