// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Remoting.Tests;

public class RemotingInstrumentationTests
{
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "Exception message")]
    public void CrossDomainMessageTest(bool success, string? exceptionMessage)
    {
        var activities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddRemotingInstrumentation(
                options =>
                {
                    options.RecordException = true;
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
                    };
                })
            .Build();
        var domainSetup = AppDomain.CurrentDomain.SetupInformation;
        var ad = AppDomain.CreateDomain("other-domain", null, domainSetup);
        try
        {
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
        }
        finally
        {
            AppDomain.Unload(ad);
        }

        Assert.Single(activities); // OnStart/OnEnd/OnShutdown/Dispose called.
        var activity = activities.FirstOrDefault(); // Get the OnEnd activity.
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("netframework_remoting", activity.GetTagItem("rpc.system.name"));
        Assert.Equal("OpenTelemetry.Instrumentation.Remoting.Tests.RemotingInstrumentationTests+RemoteObject/DoStuff", activity.GetTagItem("rpc.method"));

        if (success)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);

            var eventList = activity.Events.ToList();
            Assert.Single(eventList);

            Assert.Equal("exception", eventList[0].Name);
        }
    }

    [Fact]
    public void RemotingInstrumentation_RegisterDynamicProperty_OnlyOnce()
    {
        var options = new RemotingInstrumentationOptions();

        // This will register the dynamic property on the current context
        using var i1 = new RemotingInstrumentation(options);
        Assert.Equal(1, RemotingInstrumentation.RegCount);

        // Second call should increment count but NOT re-register the property
        using var i2 = new RemotingInstrumentation(options);
        Assert.Equal(2, RemotingInstrumentation.RegCount);
    }

    private class RemoteObject : ContextBoundObject
    {
        public void DoStuff(string? exceptionMessage = null)
        {
            if (exceptionMessage != null)
            {
                throw new Exception(exceptionMessage);
            }
        }
    }
}
