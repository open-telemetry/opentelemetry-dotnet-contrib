// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0using System.Fabric;

using System.Fabric;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// An IServiceRemotingClientFactory that uses Fabric TCP transport to create IServiceRemotingClient
/// that communicate with stateless and stateful services over interfaces that are
/// remoted via Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime.FabricTransportServiceRemotingListener.
///
/// Both the client and the listener are instrumented to propagate both the traceContext and Baggage objects from OpenTelemetry
/// </summary>
public class TraceContextEnrichedServiceRemotingClientFactory : IServiceRemotingClientFactory
{
    private readonly FabricTransportServiceRemotingClientFactory innerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceContextEnrichedServiceRemotingClientFactory"/> class.
    /// </summary>
    /// <param name="fabricTransportRemotingSettings">The settings for the fabric transport. If the settings are not provided or null, default settings with no security.</param>
    /// <param name="callbackMessageHandler">The callback client that receives the callbacks from the service.</param>
    public TraceContextEnrichedServiceRemotingClientFactory(FabricTransportRemotingSettings fabricTransportRemotingSettings, IServiceRemotingCallbackMessageHandler? callbackMessageHandler)
    {
        this.innerFactory = new FabricTransportServiceRemotingClientFactory(
           fabricTransportRemotingSettings,
           callbackMessageHandler,
           servicePartitionResolver: null,
           exceptionHandlers: null,
           traceId: null);
    }

    /// <summary>
    /// Event handler that is fired when a client is connected to the service endpoint.
    /// </summary>
    public event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ClientConnected
    {
        add { this.innerFactory.ClientConnected += value; }
        remove { this.innerFactory.ClientConnected -= value; }
    }

    /// <summary>
    /// Event handler that is fired when a client is disconnected from the service endpoint.
    /// </summary>
    public event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ClientDisconnected
    {
        add { this.innerFactory.ClientDisconnected += value; }
        remove { this.innerFactory.ClientDisconnected -= value; }
    }

    /// <summary>
    /// Gets a factory for creating the remoting message bodies.
    /// </summary>
    /// <returns>A factory for creating the remoting message bodies.</returns>
    public IServiceRemotingMessageBodyFactory GetRemotingMessageBodyFactory()
    {
        return this.innerFactory.GetRemotingMessageBodyFactory();
    }

    /// <inheritdoc/>
    public async Task<IServiceRemotingClient> GetClientAsync(Uri serviceUri, ServicePartitionKey partitionKey, TargetReplicaSelector targetReplicaSelector, string listenerName, OperationRetrySettings retrySettings, CancellationToken cancellationToken)
    {
        IServiceRemotingClient serviceRemotingClient = await this.innerFactory.GetClientAsync(
            serviceUri,
            partitionKey,
            targetReplicaSelector,
            listenerName,
            retrySettings,
            cancellationToken).ConfigureAwait(false);

        return new TraceContextEnrichedServiceRemotingClientAdapter(serviceRemotingClient);
    }

    /// <inheritdoc/>
    public async Task<IServiceRemotingClient> GetClientAsync(ResolvedServicePartition previousRsp, TargetReplicaSelector targetReplicaSelector, string listenerName, OperationRetrySettings retrySettings, CancellationToken cancellationToken)
    {
        IServiceRemotingClient serviceRemotingClient = await this.innerFactory.GetClientAsync(
            previousRsp,
            targetReplicaSelector,
            listenerName,
            retrySettings,
            cancellationToken).ConfigureAwait(false);

        return new TraceContextEnrichedServiceRemotingClientAdapter(serviceRemotingClient);
    }

    /// <inheritdoc/>
    public Task<OperationRetryControl> ReportOperationExceptionAsync(IServiceRemotingClient client, ExceptionInformation exceptionInformation, OperationRetrySettings retrySettings, CancellationToken cancellationToken)
    {
        IServiceRemotingClient innerClient = client;
        TraceContextEnrichedServiceRemotingClientAdapter? clientAdapter = client as TraceContextEnrichedServiceRemotingClientAdapter;
        if (clientAdapter != null)
        {
            innerClient = clientAdapter.InnerClient;
        }

        return this.innerFactory.ReportOperationExceptionAsync(innerClient, exceptionInformation, retrySettings, cancellationToken);
    }
}
