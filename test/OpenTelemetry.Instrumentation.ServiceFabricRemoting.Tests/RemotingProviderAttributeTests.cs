// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using ServiceFabric.Mocks;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

[Collection(ServiceFabricRemotingCollectionDefinition.Name)]
public class RemotingProviderAttributeTests
{
    private const string NativeRuntimeRequiredMessage = "Creating a Service Fabric remoting listener requires the native Service Fabric runtime, which is only available on Windows.";

    [Fact]
    public void ServiceRemotingProvider_CreatesAV2Listener()
    {
        var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();

        var listeners = provider.CreateServiceRemotingListeners();

        Assert.Equal("V2Listener", Assert.Single(listeners).Key);
    }

    [Fact]
    public void ServiceRemotingProvider_ListenerFactoryCreatesAListener()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), NativeRuntimeRequiredMessage);

        var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();
        var serviceContext = MockStatefulServiceContextFactory.Default;
        var reliableStateManager = new MockReliableStateManager();
        var service = new MyTestStatefulService(serviceContext, reliableStateManager);
        var listenerFactory = Assert.Single(provider.CreateServiceRemotingListeners()).Value;

        var listener = listenerFactory(serviceContext, service);

        Assert.NotNull(listener);
    }

    [Fact]
    public void ServiceRemotingProvider_CreatesAnInstrumentedClientFactory()
    {
        var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();

        var clientFactory = provider.CreateServiceRemotingClientFactoryV2(callbackMessageHandler: null);

        Assert.IsType<TraceContextEnrichedServiceRemotingClientFactoryAdapter>(clientFactory);
    }

    [Fact]
    public void ActorRemotingProvider_CreatesAV2Listener()
    {
        var provider = new TraceContextEnrichedActorRemotingProviderAttribute();

        var listeners = provider.CreateServiceRemotingListeners();

        Assert.Equal("V2Listener", Assert.Single(listeners).Key);
    }

    [Fact]
    public void ActorRemotingProvider_ListenerFactoryCreatesAListener()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), NativeRuntimeRequiredMessage);

        var provider = new TraceContextEnrichedActorRemotingProviderAttribute();
        var actorService = MockActorServiceFactory.CreateActorServiceForActor<MyTestActor>();
        var listenerFactory = Assert.Single(provider.CreateServiceRemotingListeners()).Value;

        var listener = listenerFactory(actorService);

        Assert.NotNull(listener);
    }

    [Fact]
    public void ActorRemotingProvider_CreatesAnInstrumentedClientFactory()
    {
        var provider = new TraceContextEnrichedActorRemotingProviderAttribute();

        var clientFactory = provider.CreateServiceRemotingClientFactory(callbackMessageHandler: null);

        Assert.IsType<TraceContextEnrichedServiceRemotingClientFactoryAdapter>(clientFactory);
    }
}
