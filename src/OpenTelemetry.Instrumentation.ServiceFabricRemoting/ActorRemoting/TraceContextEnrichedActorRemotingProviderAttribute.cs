// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.ServiceFabric.Actors.Generator;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Actors.Remoting.V2.Runtime;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using ClientExceptionConvertor = Microsoft.ServiceFabric.Services.Remoting.V2.Client.IExceptionConvertor;
using RuntimeExceptionConvertor = Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.IExceptionConvertor;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// Sets fabric TCP transport as the default remoting provider for the actors.
/// </summary>
/// <remarks>
/// This type is not sealed so that applications can derive from it to register custom exception convertors by
/// overriding <see cref="GetServiceExceptionConvertors"/> and <see cref="GetClientExceptionConvertors"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
[SuppressMessage("Performance", "CA1813:Avoid unsealed attributes", Justification = "The attribute is designed to be derived from, so that applications can register custom exception convertors.")]
public class TraceContextEnrichedActorRemotingProviderAttribute : FabricTransportActorRemotingProviderAttribute
{
    private const string DefaultV2listenerName = "V2Listener";

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceContextEnrichedActorRemotingProviderAttribute"/> class.
    /// </summary>
    public TraceContextEnrichedActorRemotingProviderAttribute()
    {
        this.RemotingClientVersion = Microsoft.ServiceFabric.Services.Remoting.RemotingClientVersion.V2;
        this.RemotingListenerVersion = Microsoft.ServiceFabric.Services.Remoting.RemotingListenerVersion.V2;
    }

    /// <summary>
    /// Gets or sets the maximum number of levels of inner exceptions that are serialized when a remoting call fails.
    /// When set to a value greater than zero it overrides the Service Fabric default.
    /// </summary>
    public int RemotingExceptionDepth { get; set; }

    /// <summary>
    ///     Creates a service remoting listener for remoting the actor interfaces.
    /// </summary>.
    /// <returns>
    ///     A <see cref="FabricTransportActorServiceRemotingListener"/> as <see cref="IServiceRemotingListener"/> for the specified actor service.
    /// </returns>
    public override Dictionary<string, Func<ActorService, IServiceRemotingListener>> CreateServiceRemotingListeners()
    {
        var dictionary = new Dictionary<string, Func<ActorService, IServiceRemotingListener>>
        {
            [DefaultV2listenerName] = (actorService) =>
            {
                var actorServiceRemotingDispatcher = new ActorServiceRemotingDispatcher(actorService, serviceRemotingRequestMessageBodyFactory: null);
                var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(actorServiceRemotingDispatcher);
                var listenerSettings = this.InitializeListenerSettings(actorService);

                return new FabricTransportActorServiceRemotingListener(
                    actorService,
                    dispatcherAdapter,
                    listenerSettings,
                    serializationProvider: null,
                    exceptionConvertors: this.GetServiceExceptionConvertors());
            },
        };

        return dictionary;
    }

    /// <summary>
    ///  Creates a service remoting client factory that can be used by the Microsoft.ServiceFabric.Services.Remoting.V2.Client.ServiceProxyFactory
    ///  to create a proxy for the remoted interface of the service.
    /// </summary>
    /// <param name="callbackMessageHandler">Client implementation where the callbacks should be dispatched.</param>
    /// <returns> An <see cref="IServiceRemotingClientFactory"/>.</returns>
    public override IServiceRemotingClientFactory CreateServiceRemotingClientFactory(IServiceRemotingCallbackMessageHandler? callbackMessageHandler)
    {
        var settings = new FabricTransportRemotingSettings();
        settings.MaxMessageSize = this.GetAndValidateMaxMessageSize(settings.MaxMessageSize);
        settings.OperationTimeout = this.GetAndValidateOperationTimeout(settings.OperationTimeout);
        settings.KeepAliveTimeout = this.GetAndValidateKeepAliveTimeout(settings.KeepAliveTimeout);
        settings.ConnectTimeout = this.GetConnectTimeout(settings.ConnectTimeout);

        var fabricTransportActorRemotingClientFactory = new FabricTransportActorRemotingClientFactory(
            settings,
            callbackMessageHandler,
            servicePartitionResolver: null,
            exceptionHandlers: null,
            traceId: null,
            serializationProvider: null,
            exceptionConvertors: this.GetClientExceptionConvertors());

        return new TraceContextEnrichedServiceRemotingClientFactoryAdapter(fabricTransportActorRemotingClientFactory);
    }

    /// <summary>
    /// Gets the exception convertors that the remoting listener uses to convert the exceptions thrown by the actor
    /// implementation into a serializable form. Override this method to support custom exception types.
    /// </summary>
    /// <remarks>
    /// Service Fabric always appends its built-in convertors, so only convertors for custom exception types need to be returned here.
    /// </remarks>
    /// <returns>The exception convertors to register with the listener, or <see langword="null"/> to register none.</returns>
    protected virtual IEnumerable<RuntimeExceptionConvertor>? GetServiceExceptionConvertors() => null;

    /// <summary>
    /// Gets the exception convertors that the remoting client uses to reconstruct the exceptions thrown by the actor
    /// implementation. Override this method to support custom exception types.
    /// </summary>
    /// <remarks>
    /// Service Fabric always appends its built-in convertors, so only convertors for custom exception types need to be returned here.
    /// </remarks>
    /// <returns>The exception convertors to register with the client factory, or <see langword="null"/> to register none.</returns>
    protected virtual IEnumerable<ClientExceptionConvertor>? GetClientExceptionConvertors() => null;

    private static FabricTransportRemotingListenerSettings GetActorListenerSettings(ActorService actorService)
    {
        var sectionName = ActorNameFormat.GetFabricServiceTransportSettingsSectionName(actorService.ActorTypeInformation.ImplementationType);

        var succeeded = FabricTransportRemotingListenerSettings.TryLoadFrom(sectionName, out var listenerSettings);
        if (!succeeded)
        {
            listenerSettings = new FabricTransportRemotingListenerSettings();
        }

        return listenerSettings;
    }

    private FabricTransportRemotingListenerSettings InitializeListenerSettings(ActorService actorService)
    {
        var listenerSettings = GetActorListenerSettings(actorService);

        listenerSettings.MaxMessageSize = this.GetAndValidateMaxMessageSize(listenerSettings.MaxMessageSize);
        listenerSettings.OperationTimeout = this.GetAndValidateOperationTimeout(listenerSettings.OperationTimeout);
        listenerSettings.KeepAliveTimeout = this.GetAndValidateKeepAliveTimeout(listenerSettings.KeepAliveTimeout);
        listenerSettings.RemotingExceptionDepth = this.GetRemotingExceptionDepth(listenerSettings.RemotingExceptionDepth);

        return listenerSettings;
    }

    private long GetAndValidateMaxMessageSize(long maxMessageSizeDefault)
        => (this.MaxMessageSize > 0) ? this.MaxMessageSize : maxMessageSizeDefault;

    private TimeSpan GetAndValidateOperationTimeout(TimeSpan operationTimeoutDefault)
        => (this.OperationTimeoutInSeconds > 0) ? TimeSpan.FromSeconds(this.OperationTimeoutInSeconds) : operationTimeoutDefault;

    private TimeSpan GetAndValidateKeepAliveTimeout(TimeSpan keepAliveTimeoutDefault)
        => (this.KeepAliveTimeoutInSeconds > 0) ? TimeSpan.FromSeconds(this.KeepAliveTimeoutInSeconds) : keepAliveTimeoutDefault;

    private TimeSpan GetConnectTimeout(TimeSpan connectTimeoutDefault)
        => (this.ConnectTimeoutInMilliseconds > 0) ? TimeSpan.FromMilliseconds(this.ConnectTimeoutInMilliseconds) : connectTimeoutDefault;

    private int GetRemotingExceptionDepth(int remotingExceptionDepthDefault)
        => (this.RemotingExceptionDepth > 0) ? this.RemotingExceptionDepth : remotingExceptionDepthDefault;
}
