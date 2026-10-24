// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

[Collection(ServiceFabricRemotingCollectionDefinition.Name)]
public class TransportSettingsHookTests
{
    private const long AttributeMaxMessageSize = 17;

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    [Fact]
    public void ServiceRemotingProvider_ConfigureListenerSettingsRunsAfterTheAttributeValuesAreApplied()
    {
        var originalLoader = TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader;

        try
        {
            TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = () => null;
            var provider = new ServiceSettingsHookAttribute { MaxMessageSize = AttributeMaxMessageSize };

            var actual = provider.GetListenerSettings();

            Assert.Equal(AttributeMaxMessageSize, provider.MaxMessageSizeSeenByListenerHook);
            Assert.Equal(FabricTransportRemotingListenerSettings.ExceptionSerialization.BinaryFormatter, actual.ExceptionSerializationTechnique);
        }
        finally
        {
            TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = originalLoader;
        }
    }

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    [Fact]
    public void ServiceRemotingProvider_ConfigureClientSettingsRunsAfterTheAttributeValuesAreApplied()
    {
        var originalLoader = TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader;

        try
        {
            TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader = () => null;
            var provider = new ServiceSettingsHookAttribute { MaxMessageSize = AttributeMaxMessageSize };

            var actual = provider.GetRemotingSettings();

            Assert.Equal(AttributeMaxMessageSize, provider.MaxMessageSizeSeenByClientHook);
            Assert.Equal(FabricTransportRemotingSettings.ExceptionDeserialization.Fallback, actual.ExceptionDeserializationTechnique);
        }
        finally
        {
            TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader = originalLoader;
        }
    }

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    [Fact]
    public void ActorRemotingProvider_ConfigureListenerSettingsRunsAfterTheAttributeValuesAreApplied()
    {
        var originalLoader = TraceContextEnrichedActorRemotingProviderAttribute.ListenerSettingsLoader;

        try
        {
            TraceContextEnrichedActorRemotingProviderAttribute.ListenerSettingsLoader = _ => null;
            var provider = new ActorSettingsHookAttribute { MaxMessageSize = AttributeMaxMessageSize };

            var actual = provider.GetListenerSettings(typeof(MyTestActor));

            Assert.Equal(AttributeMaxMessageSize, provider.MaxMessageSizeSeenByListenerHook);
            Assert.Equal(FabricTransportRemotingListenerSettings.ExceptionSerialization.BinaryFormatter, actual.ExceptionSerializationTechnique);
        }
        finally
        {
            TraceContextEnrichedActorRemotingProviderAttribute.ListenerSettingsLoader = originalLoader;
        }
    }

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    [Fact]
    public void ActorRemotingProvider_ConfigureClientSettingsRunsAfterTheAttributeValuesAreApplied()
    {
        var originalLoader = TraceContextEnrichedActorRemotingProviderAttribute.RemotingSettingsLoader;

        try
        {
            TraceContextEnrichedActorRemotingProviderAttribute.RemotingSettingsLoader = () => null;
            var provider = new ActorSettingsHookAttribute { MaxMessageSize = AttributeMaxMessageSize };

            var actual = provider.GetRemotingSettings();

            Assert.Equal(AttributeMaxMessageSize, provider.MaxMessageSizeSeenByClientHook);
            Assert.Equal(FabricTransportRemotingSettings.ExceptionDeserialization.Fallback, actual.ExceptionDeserializationTechnique);
        }
        finally
        {
            TraceContextEnrichedActorRemotingProviderAttribute.RemotingSettingsLoader = originalLoader;
        }
    }

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    [Fact]
    public void RemotingProviders_DoNotChangeTheExceptionTechniquesByDefault()
    {
        var originalServiceListenerLoader = TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader;
        var originalServiceClientLoader = TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader;
        var originalActorListenerLoader = TraceContextEnrichedActorRemotingProviderAttribute.ListenerSettingsLoader;
        var originalActorClientLoader = TraceContextEnrichedActorRemotingProviderAttribute.RemotingSettingsLoader;

        try
        {
            TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = () => null;
            TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader = () => null;
            TraceContextEnrichedActorRemotingProviderAttribute.ListenerSettingsLoader = _ => null;
            TraceContextEnrichedActorRemotingProviderAttribute.RemotingSettingsLoader = () => null;
            var serviceProvider = new TraceContextEnrichedServiceRemotingProviderAttribute();
            var actorProvider = new TraceContextEnrichedActorRemotingProviderAttribute();
            var defaultListenerSettings = new FabricTransportRemotingListenerSettings();
            var defaultClientSettings = new FabricTransportRemotingSettings();

            Assert.Equal(defaultListenerSettings.ExceptionSerializationTechnique, serviceProvider.GetListenerSettings().ExceptionSerializationTechnique);
            Assert.Equal(defaultClientSettings.ExceptionDeserializationTechnique, serviceProvider.GetRemotingSettings().ExceptionDeserializationTechnique);
            Assert.Equal(defaultListenerSettings.ExceptionSerializationTechnique, actorProvider.GetListenerSettings(typeof(MyTestActor)).ExceptionSerializationTechnique);
            Assert.Equal(defaultClientSettings.ExceptionDeserializationTechnique, actorProvider.GetRemotingSettings().ExceptionDeserializationTechnique);
        }
        finally
        {
            TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = originalServiceListenerLoader;
            TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader = originalServiceClientLoader;
            TraceContextEnrichedActorRemotingProviderAttribute.ListenerSettingsLoader = originalActorListenerLoader;
            TraceContextEnrichedActorRemotingProviderAttribute.RemotingSettingsLoader = originalActorClientLoader;
        }
    }

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    private sealed class ServiceSettingsHookAttribute : TraceContextEnrichedServiceRemotingProviderAttribute
    {
        public long MaxMessageSizeSeenByListenerHook { get; private set; }

        public long MaxMessageSizeSeenByClientHook { get; private set; }

        protected override void ConfigureListenerSettings(FabricTransportRemotingListenerSettings settings)
        {
            this.MaxMessageSizeSeenByListenerHook = settings.MaxMessageSize;
            settings.ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.BinaryFormatter;
        }

        protected override void ConfigureClientSettings(FabricTransportRemotingSettings settings)
        {
            this.MaxMessageSizeSeenByClientHook = settings.MaxMessageSize;
            settings.ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Fallback;
        }
    }

    [Obsolete("Exercises the Service Fabric exception serialization settings, which are obsolete.")]
    private sealed class ActorSettingsHookAttribute : TraceContextEnrichedActorRemotingProviderAttribute
    {
        public long MaxMessageSizeSeenByListenerHook { get; private set; }

        public long MaxMessageSizeSeenByClientHook { get; private set; }

        protected override void ConfigureListenerSettings(FabricTransportRemotingListenerSettings settings)
        {
            this.MaxMessageSizeSeenByListenerHook = settings.MaxMessageSize;
            settings.ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.BinaryFormatter;
        }

        protected override void ConfigureClientSettings(FabricTransportRemotingSettings settings)
        {
            this.MaxMessageSizeSeenByClientHook = settings.MaxMessageSize;
            settings.ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Fallback;
        }
    }
}
