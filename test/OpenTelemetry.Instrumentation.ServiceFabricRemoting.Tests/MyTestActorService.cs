// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Fabric;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Actors.Remoting.V2.Runtime;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class MyTestActorService : ActorService, IMyTestActorService
{
    private static readonly Guid ActorId = Guid.Parse("{1F263E8C-78D4-4D91-AAE6-C4B9CE03D6EB}");

    private readonly ServiceRemotingMessageDispatcherAdapter dispatcher;

    public MyTestActorService(StatefulServiceContext context, ActorTypeInformation actorTypeInfo, Func<ActorService, ActorId, ActorBase>? actorFactory = null, Func<ActorBase, IActorStateProvider, IActorStateManager>? stateManagerFactory = null, IActorStateProvider? stateProvider = null, ActorServiceSettings? settings = null)
        : base(context, actorTypeInfo, actorFactory, stateManagerFactory, stateProvider, settings)
    {
        var actorServiceRemotingDispatcher = new ActorServiceRemotingDispatcher(this, serviceRemotingRequestMessageBodyFactory: null);

        this.dispatcher = new ServiceRemotingMessageDispatcherAdapter(actorServiceRemotingDispatcher);

        var id = new ActorId(ActorId);
        this.Actor = new MyTestActor(this, id);
    }

    public IServiceRemotingMessageHandler Dispatcher => this.dispatcher;

    public MyTestActor Actor { get; }

    public Task<ServiceResponse> TestContextPropagation(string valueToReturn)
        => this.Actor.TestContextPropagation(valueToReturn);

    protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
    {
        IServiceRemotingListener CreateListener()
        {
            var listenerSettings = new FabricTransportRemotingListenerSettings();

            return new FabricTransportActorServiceRemotingListener(this, this.dispatcher, listenerSettings);
        }

        var serviceReplicaListener = new ServiceReplicaListener(t => CreateListener(), "V2Listener");

        return [serviceReplicaListener];
    }
}
