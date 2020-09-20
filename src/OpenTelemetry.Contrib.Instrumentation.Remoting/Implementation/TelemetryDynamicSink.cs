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
using System.Collections.Concurrent;
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
        internal const string AttributeRpcSystem = "rpc.system";
        internal const string AttributeRpcService = "rpc.service";
        internal const string AttributeRpcMethod = "rpc.method";

        // Uri like "tcp://localhost:1234/HelloServer.rem"
        // TODO: semantic conventions don't have an attribute for a full uri of an RPC endpoint, but seems useful?
        internal const string AttributeRpcRemotingUri = "rpc.dotnet_remoting.uri";

        internal const string AttributeExceptionType = "exception.type";
        internal const string AttributeExceptionMessage = "exception.message";
        internal const string AttributeExceptionStackTrace = "exception.stacktrace";

        internal const string ActivitySourceName = "OpenTelemetry.Remoting";
        private const string ActivityOutName = ActivitySourceName + ".RequestOut";
        private const string ActivityInName = ActivitySourceName + ".RequestIn";

        private static readonly Version Version = typeof(TelemetryDynamicSink).Assembly.GetName().Version;
        private static readonly ActivitySource RemotingActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

        private static readonly ConcurrentDictionary<string, string> ServiceNameCache = new ConcurrentDictionary<string, string>();

        private readonly RemotingInstrumentationOptions options;

        public TelemetryDynamicSink(RemotingInstrumentationOptions options)
        {
            this.options = options;
        }

        public void ProcessMessageStart(IMessage reqMsg, bool bCliSide, bool bAsync)
        {
            try
            {
                if (this.options.Filter?.Invoke(reqMsg) == false)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            // Are we executing on client?
            if (bCliSide)
            {
                // Start new outgoing activity
                var act = RemotingActivitySource.StartActivity(ActivityOutName, ActivityKind.Client);
                if (act != null)
                {
                    SetStartingActivityAttributes(act, reqMsg);

                    var callContext = (LogicalCallContext)reqMsg.Properties["__CallContext"];

                    this.options.Propagator.Inject(new PropagationContext(act.Context, Baggage.Current), callContext, InjectActivityProperties);
                }
            }
            else
            {
                // We are on server, need to start new or attach to an existing incoming activity.

                var callContext = (LogicalCallContext)reqMsg.Properties["__CallContext"];
                var activityParentContext = this.options.Propagator.Extract(default, callContext, ExtractActivityProperties);

                Activity ourActivity;

                // Do we already have an incoming activity?
                // Existing activity might be started by an instrumentation layer higher up the
                // stack. E.g. if we are using http as a transport for remoting, and we have
                // instrumented incoming http request, then that instrumentation happens before
                // our "ProcessMessageStart" and we just need to attach to that existing activity.
                var outerActivity = Activity.Current;
                if (outerActivity != null)
                {
                    ourActivity = RemotingActivitySource.StartActivity(ActivityInName, ActivityKind.Server);
                }
                else
                {
                    // We don't have an existing incoming activity. Start a brand new one ourselves.

                    ourActivity = RemotingActivitySource.StartActivity(ActivityInName, ActivityKind.Server, activityParentContext.ActivityContext);
                }

                SetStartingActivityAttributes(ourActivity, reqMsg);
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

            try
            {
                if (this.options.Filter?.Invoke(replyMsg) == false)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            var act = Activity.Current;
            if (act == null)
            {
                return;
            }

            bool validClientActivity = bCliSide && act.OperationName == ActivityOutName;
            bool validServerActivity = !bCliSide && act.OperationName == ActivityInName;
            if (validClientActivity || validServerActivity)
            {
                if (replyMsg is IMethodReturnMessage returnMsg)
                {
                    if (returnMsg.Exception == null)
                    {
                        act.SetStatus(Status.Ok);
                    }
                    else
                    {
                        act.SetStatus(Status.Unknown);

                        var e = returnMsg.Exception;
                        act.SetTag(AttributeExceptionType, e.GetType().FullName);
                        act.SetTag(AttributeExceptionMessage, e.Message);
                        act.SetTag(AttributeExceptionStackTrace, e.ToString());
                    }
                }

                act.Stop();
            }
        }

        private static void SetStartingActivityAttributes(Activity activity, IMessage msg)
        {
            if (activity != null && activity.IsAllDataRequested)
            {
                string serviceName = GetServiceName((string)msg.Properties["__TypeName"]);
                string methodName = (string)msg.Properties["__MethodName"];
                activity.DisplayName = $"{serviceName}/{methodName}";
                activity.SetTag(AttributeRpcSystem, "netframework_remoting");
                activity.SetTag(AttributeRpcService, serviceName);
                activity.SetTag(AttributeRpcMethod, methodName);

                var uriString = (string)msg.Properties["__Uri"];
                activity.SetTag(AttributeRpcRemotingUri, uriString);
            }
        }

        private static string GetServiceName(string typeName)
        {
            // typeName will be a full .NET type name as a string "SharedLib.IHelloServer, SharedLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
            return ServiceNameCache.GetOrAdd(typeName, s =>
                {
                    int pos = s.IndexOf(",", StringComparison.OrdinalIgnoreCase);
                    if (pos >= 0)
                    {
                        return s.Substring(0, pos);
                    }

                    return s;
                });
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
