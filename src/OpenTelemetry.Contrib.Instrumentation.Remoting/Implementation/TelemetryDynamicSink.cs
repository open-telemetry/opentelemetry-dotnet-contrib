// <copyright file="TelemetryDynamicSink.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Instrumentation.Remoting.Implementation
{
    /// <summary>
    /// A dynamic remoting sink that intercepts all calls leaving or entering an AppDomain, see
    /// <see cref="TracerProviderBuilderExtensions.AddRemotingInstrumentation"/> RegisterDynamicProperty
    /// code.
    /// </summary>
    /// <remarks>
    /// See https://docs.microsoft.com/en-us/previous-versions/dotnet/netframework-4.0/tdzwhfy3(v=vs.100) for
    /// information about remoting sinks and https://docs.microsoft.com/en-us/dotnet/api/system.runtime.remoting.contexts.context.registerdynamicproperty?view=netframework-4.8
    /// for RegisterDynamicProperty behavior.
    /// </remarks>
    internal class TelemetryDynamicSink : IDynamicMessageSink
    {
        internal const string ActivitySourceName = "OpenTelemetry.Remoting";
        private const string ActivityOutName = ActivitySourceName + ".RequestOut";
        private const string ActivityInName = ActivitySourceName + ".RequestIn";

        // TODO: make configurable?
        private static readonly IPropagator Propagator = new TextMapPropagator();

        private static readonly Version Version = typeof(TelemetryDynamicSink).Assembly.GetName().Version;
        private static readonly ActivitySource RemotingActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

        public void ProcessMessageStart(IMessage reqMsg, bool bCliSide, bool bAsync)
        {
            // Are we executing on client?
            if (bCliSide)
            {
                // Start new outgoing activity
                var act = RemotingActivitySource.StartActivity(ActivityOutName, ActivityKind.Client);
                if (act != null)
                {
                    act.SetTag("uri", reqMsg.Properties["__Uri"]);

                    // TODO: other useful tags (method name? parameters?)

                    var callContext = (LogicalCallContext)reqMsg.Properties["__CallContext"];

                    Propagator.Inject(new PropagationContext(act.Context, Baggage.Current), callContext, InjectActivityProperties);
                }
            }
            else
            {
                // We are on server, need to start new or attach to an existing incoming activity.

                // Do we already have an incoming activity?
                // Existing activity might be started by an instrumentation layer higher up the
                // stack. E.g. if we are using http as a transport for remoting, and we have
                // instrumented incoming http request, then that instrumentation happens before
                // our "ProcessMessageStart" and we just need to attach to that existing activity.
                var act = Activity.Current;
                if (act != null)
                {
                    RemotingActivitySource.StartActivity(ActivityInName, ActivityKind.Server);

                    // TODO: "merge" things that have been passed via __CallContext, add useful remoting tags
                }
                else
                {
                    // We don't have an existing incoming activity. Start a brand new one ourselves.

                    var callContext = (LogicalCallContext)reqMsg.Properties["__CallContext"];

                    var activityParentContext = Propagator.Extract(default, callContext, ExtractActivityProperties);

                    RemotingActivitySource.StartActivity(ActivityInName, ActivityKind.Server, activityParentContext.ActivityContext);
                }
            }
        }

        public void ProcessMessageFinish(IMessage replyMsg, bool bCliSide, bool bAsync)
        {
            // This will be called:
            // 1) On the server after we are done processing the call and are returning back to client
            //    => Current activity should be the "ActivityIn", we need to stop it
            //
            // 2) On the client, when the server call returns
            //    => Current activity should be the "ActivityOut", we need to stop it as well

            // TODO: is this wrong? Don't just stop _any_ current activity, check if it is either of the above explicitly?
            var act = Activity.Current;
            act?.Stop();
        }

        private static void InjectActivityProperties(LogicalCallContext ctx, string key, string value)
        {
            ctx.SetData(key, value);
        }

        private static IEnumerable<string> ExtractActivityProperties(LogicalCallContext ctx, string key)
        {
            var data = ctx.GetData(key);
            if (data != null)
            {
                return new[] { (string)data };
            }

            return Enumerable.Empty<string>();
        }
    }
}
