// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Fabric;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.RemotingV2;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class TraceContextEnrichedServiceRemotingClientFactoryAdapterTests
{
    [Fact]
    public async Task GetClientAsync_WithServiceUri_WrapsTheClientCreatedByTheInnerFactory()
    {
        var innerFactory = CreateInnerFactory();
        var factory = new TraceContextEnrichedServiceRemotingClientFactoryAdapter(innerFactory);
        var serviceUri = new Uri("fabric:/MyApp/MyService");
        var partitionKey = new ServicePartitionKey(7);
        var retrySettings = new OperationRetrySettings();

        var client = await factory.GetClientAsync(serviceUri, partitionKey, TargetReplicaSelector.PrimaryReplica, listenerName: "MyListener", retrySettings, CancellationToken.None);

        var clientAdapter = Assert.IsType<TraceContextEnrichedServiceRemotingClientAdapter>(client);
        var innerClient = Assert.IsType<MockActorServiceRemotingClient>(clientAdapter.InnerClient);
        Assert.Same(partitionKey, innerClient.PartitionKey);
        Assert.Equal(TargetReplicaSelector.PrimaryReplica, innerClient.TargetReplicaSelector);
        Assert.Equal("MyListener", innerClient.ListenerName);
        Assert.Same(retrySettings, innerClient.RetrySettings);
    }

    [Fact]
    public async Task GetClientAsync_WithPreviousResolvedPartition_WrapsTheClientCreatedByTheInnerFactory()
    {
        var innerFactory = CreateInnerFactory();
        var factory = new TraceContextEnrichedServiceRemotingClientFactoryAdapter(innerFactory);
        var previousRsp = CreateSingletonResolvedServicePartition();
        var retrySettings = new OperationRetrySettings();

        var client = await factory.GetClientAsync(previousRsp, TargetReplicaSelector.RandomSecondaryReplica, listenerName: "MyListener", retrySettings, CancellationToken.None);

        var clientAdapter = Assert.IsType<TraceContextEnrichedServiceRemotingClientAdapter>(client);
        var innerClient = Assert.IsType<MockActorServiceRemotingClient>(clientAdapter.InnerClient);
        Assert.Equal(TargetReplicaSelector.RandomSecondaryReplica, innerClient.TargetReplicaSelector);
        Assert.Equal("MyListener", innerClient.ListenerName);
        Assert.Same(retrySettings, innerClient.RetrySettings);
    }

    [Fact]
    public async Task ReportOperationExceptionAsync_UnwrapsClientsCreatedByTheAdapter()
    {
        var innerFactory = CreateInnerFactory();
        var factory = new TraceContextEnrichedServiceRemotingClientFactoryAdapter(innerFactory);
        var serviceUri = new Uri("fabric:/MyApp/MyService");
        var getClientRetrySettings = new OperationRetrySettings();
        var client = await factory.GetClientAsync(serviceUri, ServicePartitionKey.Singleton, TargetReplicaSelector.Default, listenerName: "MyListener", getClientRetrySettings, CancellationToken.None);
        var innerClient = ((TraceContextEnrichedServiceRemotingClientAdapter)client).InnerClient;
        var retryControl = new OperationRetryControl();
        innerFactory.RegisterOperationRetryControl(innerClient, retryControl);
        var exception = new InvalidOperationException("Failure");
        var exceptionInformation = new ExceptionInformation(exception);
        var retrySettings = new OperationRetrySettings();

        var actual = await factory.ReportOperationExceptionAsync(client, exceptionInformation, retrySettings, CancellationToken.None);

        Assert.Same(retryControl, actual);
        var reported = Assert.Single(innerFactory.ReportedExceptionInformation);
        Assert.Same(innerClient, reported.Key);
        Assert.Same(exceptionInformation, reported.Value);
    }

    [Fact]
    public async Task ReportOperationExceptionAsync_PassesThroughOtherClients()
    {
        var actorService = MockActorServiceFactory.CreateActorServiceForActor<MyTestActor>();
        var innerFactory = new MockActorServiceRemotingClientFactory(actorService);
        var factory = new TraceContextEnrichedServiceRemotingClientFactoryAdapter(innerFactory);
        var otherClient = new MockActorServiceRemotingClient(actorService);
        var exception = new InvalidOperationException("Failure");
        var exceptionInformation = new ExceptionInformation(exception);
        var retrySettings = new OperationRetrySettings();

        await factory.ReportOperationExceptionAsync(otherClient, exceptionInformation, retrySettings, CancellationToken.None);

        var reported = Assert.Single(innerFactory.ReportedExceptionInformation);
        Assert.Same(otherClient, reported.Key);
    }

    [Fact]
    public void GetRemotingMessageBodyFactory_ReturnsTheInnerFactoryMessageBodyFactory()
    {
        var messageBodyFactory = new MockServiceRemotingMessageBodyFactory();
        var innerFactory = CreateInnerFactory();
        innerFactory.MockServiceRemotingMessageBodyFactory = messageBodyFactory;
        var factory = new TraceContextEnrichedServiceRemotingClientFactoryAdapter(innerFactory);

        Assert.Same(messageBodyFactory, factory.GetRemotingMessageBodyFactory());
    }

    [Fact]
    public void ClientEvents_AreForwardedToTheInnerFactory()
    {
        var innerFactory = CreateInnerFactory();
        var factory = new TraceContextEnrichedServiceRemotingClientFactoryAdapter(innerFactory);
        var eventArgs = new CommunicationClientEventArgs<IServiceRemotingClient>();
        var connectedCount = 0;
        var disconnectedCount = 0;
        EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> onConnected = (sender, args) => connectedCount++;
        EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> onDisconnected = (sender, args) => disconnectedCount++;

        factory.ClientConnected += onConnected;
        factory.ClientDisconnected += onDisconnected;
        innerFactory.OnClientConnected(eventArgs);
        innerFactory.OnClientDisconnected(eventArgs);

        factory.ClientConnected -= onConnected;
        factory.ClientDisconnected -= onDisconnected;
        innerFactory.OnClientConnected(eventArgs);
        innerFactory.OnClientDisconnected(eventArgs);

        Assert.Equal(1, connectedCount);
        Assert.Equal(1, disconnectedCount);
    }

    private static MockActorServiceRemotingClientFactory CreateInnerFactory()
    {
        var actorService = MockActorServiceFactory.CreateActorServiceForActor<MyTestActor>();

        return new MockActorServiceRemotingClientFactory(actorService);
    }

    private static ResolvedServicePartition CreateSingletonResolvedServicePartition()
    {
        // ResolvedServicePartition has no public constructor or setters, and the mock factory reads its partition information.
        var resolvedServicePartition = (ResolvedServicePartition)Activator.CreateInstance(typeof(ResolvedServicePartition), nonPublic: true)!;
        var partitionInformation = new SingletonPartitionInformation();
        var serviceName = new Uri("fabric:/MyApp/MyService");

        typeof(ResolvedServicePartition).GetProperty(nameof(ResolvedServicePartition.Info))!.SetValue(resolvedServicePartition, partitionInformation);
        typeof(ResolvedServicePartition).GetProperty(nameof(ResolvedServicePartition.ServiceName))!.SetValue(resolvedServicePartition, serviceName);

        return resolvedServicePartition;
    }
}
