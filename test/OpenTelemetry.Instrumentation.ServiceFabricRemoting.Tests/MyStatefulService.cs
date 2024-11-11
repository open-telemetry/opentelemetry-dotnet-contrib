// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Fabric;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class MyStatefulService : StatefulService, IMyStatefulService
{
    private TraceContextEnrichedServiceV2RemotingDispatcher dispatcher;

    public MyStatefulService(StatefulServiceContext serviceContext, IReliableStateManagerReplica reliableStateManagerReplica)
        : base(serviceContext, reliableStateManagerReplica)
    {
    }

    public Task<ServiceResponse> TestContextPropagation(string valueToReturn)
    {
        ActivityContext activityContext = Activity.Current!.Context;
        Baggage baggage = Baggage.Current;

        ServiceResponse serviceResponse = new ServiceResponse
        {
            ParameterValue = valueToReturn,
            ActivityContext = activityContext,
            Baggage = baggage,
        };

        return Task.FromResult(serviceResponse);
    }

    internal void SetDispatcher(TraceContextEnrichedServiceV2RemotingDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
    {
        Func<ServiceContext, IService, IServiceRemotingListener> getListenerFunc = (ServiceContext serviceContext, IService serviceImplementation) =>
        {
            FabricTransportRemotingListenerSettings listenerSettings = new FabricTransportRemotingListenerSettings();

            return new FabricTransportServiceRemotingListener(serviceContext, this.dispatcher, listenerSettings);
        };

        ServiceReplicaListener serviceReplicaListener = new ServiceReplicaListener((StatefulServiceContext t) => getListenerFunc(this.ServiceContext, this), "V2Listener");

        return [serviceReplicaListener];
    }
}
