// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using ClientExceptionConvertor = Microsoft.ServiceFabric.Services.Remoting.V2.Client.IExceptionConvertor;
using RuntimeExceptionConvertor = Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.IExceptionConvertor;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting;

/// <summary>
/// This attributes allows to set Fabric TCP transport as the default service remoting transport provider in the assembly and customization of it.
/// </summary>
/// <remarks>
/// This type is not sealed so that applications can derive from it to register custom exception convertors by
/// overriding <see cref="GetServiceExceptionConvertors"/> and <see cref="GetClientExceptionConvertors"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
[SuppressMessage("Performance", "CA1813:Avoid unsealed attributes", Justification = "The attribute is designed to be derived from, so that applications can register custom exception convertors.")]
public class TraceContextEnrichedServiceRemotingProviderAttribute : FabricTransportServiceRemotingProviderAttribute
{
    private const string DefaultV2listenerName = "V2Listener";
    private const string DefaultTransportSettingsSectionName = "TransportSettings";

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceContextEnrichedServiceRemotingProviderAttribute"/> class.
    /// </summary>
    public TraceContextEnrichedServiceRemotingProviderAttribute()
    {
        this.RemotingClientVersion = RemotingClientVersion.V2;
        this.RemotingListenerVersion = RemotingListenerVersion.V2;
    }

    /// <summary>
    /// Gets or sets the maximum number of levels of inner exceptions that are serialized when a remoting call fails.
    /// When set to a value greater than zero it overrides the Service Fabric default.
    /// </summary>
    public int RemotingExceptionDepth { get; set; }

    internal static Func<FabricTransportRemotingSettings?> RemotingSettingsLoader { get; set; } = LoadRemotingSettings;

    internal static Func<FabricTransportRemotingListenerSettings?> ListenerSettingsLoader { get; set; } = LoadListenerSettings;

    /// <summary>
    /// Creates a V2 service remoting listener for remoting the service interface.
    /// </summary>
    /// <returns>
    ///  A Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime.FabricTransportServiceRemotingListener
    ///  as Microsoft.ServiceFabric.Services.Remoting.Runtime.IServiceRemotingListener for the specified service implementation.
    /// </returns>
    public override Dictionary<string, Func<ServiceContext, IService, IServiceRemotingListener>> CreateServiceRemotingListeners()
    {
        var dictionary = new Dictionary<string, Func<ServiceContext, IService, IServiceRemotingListener>>
        {
            [DefaultV2listenerName] = (serviceContext, serviceImplementation) =>
            {
                var listenerSettings = this.GetListenerSettings();
                var serviceRemotingMessageDispatcher = new ServiceRemotingMessageDispatcher(serviceContext, serviceImplementation);
                var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(serviceRemotingMessageDispatcher);

                return new FabricTransportServiceRemotingListener(
                    serviceContext,
                    dispatcherAdapter,
                    listenerSettings,
                    serializationProvider: null,
                    exceptionConvertors: this.GetServiceExceptionConvertors());
            },
        };

        return dictionary;
    }

    /// <summary>
    /// Creates a V2 service remoting client factory for connecting to the service over remoted service interfaces.
    /// </summary>
    /// <param name="callbackMessageHandler"> The client implementation where the callbacks should be dispatched.</param>
    /// <returns>
    /// A Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client.FabricTransportServiceRemotingClientFactory
    /// as Microsoft.ServiceFabric.Services.Remoting.V2.Client.IServiceRemotingClientFactory
    /// that can be used with Microsoft.ServiceFabric.Services.Remoting.Client.ServiceProxyFactory
    /// to generate service proxy to talk to a stateless or stateful service over remoted actor interface.
    /// </returns>
    public override IServiceRemotingClientFactory CreateServiceRemotingClientFactoryV2(IServiceRemotingCallbackMessageHandler? callbackMessageHandler)
    {
        var fabricTransportRemotingSettings = this.GetRemotingSettings();

        var fabricTransportServiceRemotingClientFactory = new FabricTransportServiceRemotingClientFactory(
            fabricTransportRemotingSettings,
            callbackMessageHandler,
            servicePartitionResolver: null,
            exceptionHandlers: null,
            traceId: null,
            serializationProvider: null,
            exceptionConvertors: this.GetClientExceptionConvertors());

        return new TraceContextEnrichedServiceRemotingClientFactoryAdapter(fabricTransportServiceRemotingClientFactory);
    }

    internal FabricTransportRemotingListenerSettings GetListenerSettings()
    {
        var settings = ListenerSettingsLoader() ?? new FabricTransportRemotingListenerSettings();

        settings.MaxMessageSize = this.GetAndValidateMaxMessageSize(settings.MaxMessageSize);
        settings.OperationTimeout = this.GetAndValidateOperationTimeout(settings.OperationTimeout);
        settings.KeepAliveTimeout = this.GetAndValidateKeepAliveTimeout(settings.KeepAliveTimeout);
        settings.RemotingExceptionDepth = this.GetRemotingExceptionDepth(settings.RemotingExceptionDepth);

        return settings;
    }

    internal FabricTransportRemotingSettings GetRemotingSettings()
    {
        var settings = RemotingSettingsLoader() ?? new FabricTransportRemotingSettings();

        settings.MaxMessageSize = this.GetAndValidateMaxMessageSize(settings.MaxMessageSize);
        settings.OperationTimeout = this.GetAndValidateOperationTimeout(settings.OperationTimeout);
        settings.KeepAliveTimeout = this.GetAndValidateKeepAliveTimeout(settings.KeepAliveTimeout);
        settings.ConnectTimeout = this.GetConnectTimeout(settings.ConnectTimeout);

        return settings;
    }

    /// <summary>
    /// Gets the exception convertors that the remoting listener uses to convert the exceptions thrown by the service
    /// implementation into a serializable form. Override this method to support custom exception types.
    /// </summary>
    /// <remarks>
    /// Service Fabric always appends its built-in convertors, so only convertors for custom exception types need to be returned here.
    /// </remarks>
    /// <returns>The exception convertors to register with the listener, or <see langword="null"/> to register none.</returns>
    protected virtual IEnumerable<RuntimeExceptionConvertor>? GetServiceExceptionConvertors() => null;

    /// <summary>
    /// Gets the exception convertors that the remoting client uses to reconstruct the exceptions thrown by the service
    /// implementation. Override this method to support custom exception types.
    /// </summary>
    /// <remarks>
    /// Service Fabric always appends its built-in convertors, so only convertors for custom exception types need to be returned here.
    /// </remarks>
    /// <returns>The exception convertors to register with the client factory, or <see langword="null"/> to register none.</returns>
    protected virtual IEnumerable<ClientExceptionConvertor>? GetClientExceptionConvertors() => null;

    private static FabricTransportRemotingSettings? LoadRemotingSettings() =>
        TryLoadSettings(() =>
            FabricTransportRemotingSettings.TryLoadFrom(DefaultTransportSettingsSectionName, out var settings, filepath: null, configPackageName: null)
                ? settings
                : null);

    private static FabricTransportRemotingListenerSettings? LoadListenerSettings() =>
        TryLoadSettings(() =>
            FabricTransportRemotingListenerSettings.TryLoadFrom(DefaultTransportSettingsSectionName, out var settings, configPackageName: null)
                ? settings
                : null);

    private static T? TryLoadSettings<T>(Func<T?> loader)
        where T : class
    {
        try
        {
            return loader();
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
        {
            return null;
        }
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
