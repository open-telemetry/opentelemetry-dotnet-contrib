// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.Remoting.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Remoting.Implementation;
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
                    // xUnit runner uses AppDomains to execute tests.
                    // Without the Filter, we would start instrumenting all cross-domain messages, including the xUnit runner ones.
                    // We don't want this obviously, instead just inspect calls to our test object only.
                    options.Filter = message => message is IMethodMessage methodMsg && methodMsg.TypeName.Contains("RemoteObject");
                })
            .Build();

        var domainSetup = AppDomain.CurrentDomain.SetupInformation;
        var appDomain = AppDomain.CreateDomain("other-domain", null, domainSetup);

        try
        {
            var remoteObjectTypeName = typeof(RemoteObject).FullName;
            Assert.NotNull(remoteObjectTypeName);

            var obj = (RemoteObject)appDomain.CreateInstanceAndUnwrap(
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
            AppDomain.Unload(appDomain);
        }

        Assert.Single(activities); // OnStart/OnEnd/OnShutdown/Dispose called.
        var activity = activities.FirstOrDefault(); // Get the OnEnd activity.
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("dotnet.remoting", activity.GetTagItem(SemanticConventions.AttributeRpcSystemName));
        Assert.Equal("OpenTelemetry.Instrumentation.Remoting.Tests.RemotingInstrumentationTests+RemoteObject/DoStuff", activity.GetTagItem(SemanticConventions.AttributeRpcMethod));

        Assert.Null(activity.GetTagItem(SemanticConventions.AttributeServerAddress));
        Assert.Null(activity.GetTagItem(SemanticConventions.AttributeServerPort));

        if (success)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
            Assert.Null(activity.GetTagItem(SemanticConventions.AttributeErrorType));
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal(typeof(Exception).FullName, activity.GetTagItem(SemanticConventions.AttributeErrorType));
            Assert.Empty(activity.Events);
        }
    }

    [Fact]
    public void RemotingInstrumentation_RegisterDynamicProperty_OnlyOnce()
    {
        var options = new TestOptionsMonitor<RemotingInstrumentationOptions>(new RemotingInstrumentationOptions());

        // This will register the dynamic property on the current context
        using var i1 = new RemotingInstrumentation(options);
        Assert.Equal(1, RemotingInstrumentation.RegistrationCount);

        // Second call should increment count but NOT re-register the property
        using var i2 = new RemotingInstrumentation(options);
        Assert.Equal(2, RemotingInstrumentation.RegistrationCount);
    }

    [Fact]
    public void RemotingInstrumentation_HonorsReloadedOptions()
    {
        var activities = new List<Activity>();
        var optionsMonitor = new TestOptionsMonitor<RemotingInstrumentationOptions>(
            new RemotingInstrumentationOptions
            {
                Filter = _ => false,
            },
            updateCurrentValueOnSet: false);

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .ConfigureServices(services => services.AddSingleton<IOptionsMonitor<RemotingInstrumentationOptions>>(optionsMonitor))
            .AddRemotingInstrumentation()
            .Build();

        InvokeRemoteObject();

        Assert.Empty(activities);

        optionsMonitor.Set(new RemotingInstrumentationOptions
        {
            Filter = message => message is IMethodMessage methodMessage && methodMessage.TypeName.Contains("RemoteObject"),
        });

        InvokeRemoteObject();

        Assert.Single(activities);
    }

    [Fact]
    public void RemotingInstrumentation_EnrichCallbacks_HonorSpecificMessageTypes()
    {
        var activities = new List<Activity>();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .AddRemotingInstrumentation(options =>
            {
                options.Filter = message => message is IMethodMessage methodMessage && methodMessage.TypeName.Contains("RemoteObject");
                options.EnrichWithMethodMessage = (activity, message) => activity.SetTag("remoting.start.method", message.MethodName);
                options.EnrichWithMethodReturnMessage = (activity, message) => activity.SetTag("remoting.finish.method", message.MethodName);
            })
            .Build();

        InvokeRemoteObject();

        var activity = Assert.Single(activities);
        Assert.Equal(TelemetryDynamicSinkProvider.ActivitySourceName, activity.Source.Name);
        Assert.Equal("DoStuff", activity.GetTagItem("remoting.start.method"));
        Assert.Equal("DoStuff", activity.GetTagItem("remoting.finish.method"));
    }

    [Theory]
    [InlineData("SharedLib.IHelloServer, SharedLib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "SharedLib.IHelloServer")]
    [InlineData("SharedLib.IHelloServer", "SharedLib.IHelloServer")]
    [InlineData("Company.Division.Library.MyClass", "Company.Division.Library.MyClass")]
    [InlineData("Company.Division.Library.MyClass, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c", "Company.Division.Library.MyClass")]
    [InlineData("Company.Division.Library.MyClass+AnotherNestedClass", "Company.Division.Library.MyClass+AnotherNestedClass")]
    [InlineData("Company.Division.Library.MyClass+AnotherNestedClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c", "Company.Division.Library.MyClass+AnotherNestedClass")]
    [InlineData("Company.Division.Library.MyClass+MyNestedClass`2+MyInnerNestedClass`2[[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass+AnotherNestedClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c]]", "Company.Division.Library.MyClass+MyNestedClass`2+MyInnerNestedClass`2[[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass+AnotherNestedClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c]]")]
    [InlineData("Company.Division.Library.MyClass+MyNestedClass`2+MyInnerNestedClass`2[[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass+AnotherNestedClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c]], Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c", "Company.Division.Library.MyClass+MyNestedClass`2+MyInnerNestedClass`2[[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass+AnotherNestedClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c],[Company.Division.Library.MyClass, Company.Division.Library, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7bd6737fe5b67e3c]]")]
    public void TelemetryDynamicSinkProvider_ExtractServiceName_HandlesQualifiedAndPlainTypeNames(string typeName, string expectedServiceName) =>
        Assert.Equal(expectedServiceName, TelemetryDynamicSinkProvider.ExtractServiceName(typeName));

    [Fact]
    public void TelemetryDynamicSinkProvider_BoundsServiceNameCache()
    {
        using var provider = new TelemetryDynamicSinkProvider(
            new TestOptionsMonitor<RemotingInstrumentationOptions>(
                new RemotingInstrumentationOptions()));

        string? lastResolvedServiceName = null;

        for (int i = 0; i < TelemetryDynamicSinkProvider.MaxCachedServiceNames + 32; i++)
        {
            lastResolvedServiceName = provider.GetServiceName($"Namespace.Type{i}, Assembly{i}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
        }

        Assert.Equal($"Namespace.Type{TelemetryDynamicSinkProvider.MaxCachedServiceNames + 31}", lastResolvedServiceName);
        Assert.Equal(TelemetryDynamicSinkProvider.MaxCachedServiceNames, provider.CachedServiceNameCount);
    }

    private static void InvokeRemoteObject()
    {
        var domainSetup = AppDomain.CurrentDomain.SetupInformation;
        var appDomain = AppDomain.CreateDomain("other-domain", null, domainSetup);

        try
        {
            var remoteObjectTypeName = typeof(RemoteObject).FullName;
            Assert.NotNull(remoteObjectTypeName);

            var obj = (RemoteObject)appDomain.CreateInstanceAndUnwrap(
                typeof(RemoteObject).Assembly.FullName,
                remoteObjectTypeName);

            obj.DoStuff();
        }
        finally
        {
            AppDomain.Unload(appDomain);
        }
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

    private sealed class TestOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
    {
        private readonly List<Action<TOptions, string?>> listeners = [];
        private readonly bool updateCurrentValueOnSet;

        public TestOptionsMonitor(TOptions currentValue, bool updateCurrentValueOnSet = true)
        {
            this.CurrentValue = currentValue;
            this.updateCurrentValueOnSet = updateCurrentValueOnSet;
        }

        public TOptions CurrentValue { get; private set; }

        public TOptions Get(string? name) => this.CurrentValue;

        public IDisposable OnChange(Action<TOptions, string?> listener)
        {
            this.listeners.Add(listener);
            return new CallbackDisposable(() => this.listeners.Remove(listener));
        }

        public void Set(TOptions value)
        {
            if (this.updateCurrentValueOnSet)
            {
                this.CurrentValue = value;
            }

            foreach (var listener in this.listeners)
            {
                listener(value, null);
            }
        }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action callback;

        public CallbackDisposable(Action callback)
        {
            this.callback = callback;
        }

        public void Dispose() => this.callback();
    }
}
