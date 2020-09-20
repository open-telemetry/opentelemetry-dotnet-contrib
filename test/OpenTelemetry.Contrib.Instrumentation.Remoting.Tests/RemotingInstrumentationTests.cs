// <copyright file="RemotingInstrumentationTests.cs" company="OpenTelemetry Authors">
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
using System.Runtime.Remoting.Messaging;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Contrib.Instrumentation.Remoting.Tests
{
    public class RemotingInstrumentationTests
    {
        [Theory]
        [InlineData(true, null)]
        [InlineData(false, "Exception message")]
        public void CrossDomainMessageTest(bool success, string exceptionMessage)
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddRemotingInstrumentation(options =>
                    options.Filter = msg =>
                    {
                        // xUnit runner uses AppDomains to execute tests.
                        // Without the Filter, we would start instrumenting all cross-domain messages, including the xUnit runner ones.
                        // We don't want this obviously, instead just inspect calls to our test object only.
                        if (msg is IMethodMessage methodMsg)
                        {
                            return methodMsg.TypeName.Contains("RemoteObject");
                        }

                        return false;
                    })
                .Build())
            {
                var domainSetup = AppDomain.CurrentDomain.SetupInformation;

                // When using multiple AppDomains in a single process, the remote object must either be in a separate assembly,
                // or the currently executing assembly must be loaded into another domain with shadow copy = true,
                // otherwise IDynamicMessageSink is not called.
                domainSetup.ShadowCopyFiles = "true";
                var ad = AppDomain.CreateDomain("other-domain", null, domainSetup);

                var remoteObjectTypeName = typeof(RemoteObject).FullName;
                Assert.NotNull(remoteObjectTypeName);
                var obj = (RemoteObject)ad.CreateInstanceAndUnwrap(
                    typeof(RemoteObject).Assembly.FullName,
                    remoteObjectTypeName);

                if (success)
                {
                    obj.DoStuff();
                }
                else
                {
                    Assert.Throws<Exception>(() => obj.DoStuff(exceptionMessage));
                }

                AppDomain.Unload(ad);
            }

            Assert.Equal(4, activityProcessor.Invocations.Count); // OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal("netframework_remoting", GetTag(activity, "rpc.system"));
            Assert.Equal("OpenTelemetry.Contrib.Instrumentation.Remoting.Tests.RemotingInstrumentationTests+RemoteObject", GetTag(activity, "rpc.service"));
            Assert.Equal("DoStuff", GetTag(activity, "rpc.method"));

            if (success)
            {
                Assert.Equal("Ok", GetTag(activity, SpanAttributeConstants.StatusCodeKey));
            }
            else
            {
                Assert.Equal("Unknown", GetTag(activity, SpanAttributeConstants.StatusCodeKey));
                Assert.Equal("System.Exception", GetTag(activity, "exception.type"));
                Assert.Equal(exceptionMessage, GetTag(activity, "exception.message"));
                Assert.Contains("DoStuff(String exceptionMessage)", GetTag(activity, "exception.stacktrace"));
            }
        }

        private static string GetTag(Activity act, string key)
        {
            foreach (var tag in act.Tags)
            {
                if (tag.Key == key)
                {
                    return tag.Value;
                }
            }

            return null;
        }

        private class RemoteObject : MarshalByRefObject
        {
            public void DoStuff(string exceptionMessage = null)
            {
                if (exceptionMessage != null)
                {
                    throw new Exception(exceptionMessage);
                }
            }
        }
    }
}
