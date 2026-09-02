// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Fabric;
using System.Text;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Communication;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.RemotingV2;
using ClientExceptionConvertor = Microsoft.ServiceFabric.Services.Remoting.V2.Client.IExceptionConvertor;
using RuntimeExceptionConvertor = Microsoft.ServiceFabric.Services.Remoting.V2.Runtime.IExceptionConvertor;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class ServiceFabricRemotingTests
{
    private const string ValueToSend = "SomeValue";
    private const string BaggageKey = "SomeBaggageKey";
    private const string BaggageValue = "SomeBaggageValue";
    private static readonly ActivitySource ActivitySource = new("ServiceFabricRemotingTests");
    private static readonly Lock TransportSettingsLock = new();

    [Fact]
    public async Task TestStatefulServiceContextPropagation_ShouldExtractActivityContextAndBaggage()
    {
        // Arrange
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .Build();

        var propagator = Propagators.DefaultTextMapPropagator;

        var serviceContext = MockStatefulServiceContextFactory.Default;
        var reliableStateManager = new MockReliableStateManager();

        // We need to create the service, then the dispatcher, and then set the dispatcher on the service, because the dispatcher needs the service as an argument, and the service needs the dispatcher.
        var myStatefulService = new MyTestStatefulService(serviceContext, reliableStateManager);
        var serviceRemotingMessageDispatcher = new ServiceRemotingMessageDispatcher(serviceContext, myStatefulService);
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(serviceRemotingMessageDispatcher);
        myStatefulService.SetDispatcher(dispatcherAdapter);

        // We create an ActivityContext and Baggage to inject into the request message, instead of starting a new Activity, because the dispatcher is in the same process as the test, and we don't want to set Activity.Current.
        var activityContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var baggage = Baggage.Create(new Dictionary<string, string> { { BaggageKey, BaggageValue } });

        IServiceRemotingRequestMessageHeader remotingRequestMessageHeader = this.CreateServiceRemotingRequestMessageHeader(typeof(ITestMyStatefulService), nameof(ITestMyStatefulService.TestContextPropagation));

        propagator.Inject(new PropagationContext(activityContext, baggage), remotingRequestMessageHeader, ServiceFabricRemotingUtils.InjectTraceContextIntoServiceRemotingRequestMessageHeader);

        var messageBody = new MockServiceRemotingRequestMessageBody();
        messageBody.SetParameter(0, "valueToReturn", ValueToSend);

        ServiceRemotingRequestMessageMock requestMessage = new(remotingRequestMessageHeader, messageBody);
        var remotingRequestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        var response = await dispatcherAdapter.HandleRequestResponseAsync(remotingRequestContext, requestMessage);

        // Assert
        var serviceResponse = (ServiceResponse)response.GetBody().Get(typeof(ServiceResponse));

        Assert.Equal(ValueToSend, serviceResponse.ParameterValue);
        Assert.Equal(activityContext.TraceId, serviceResponse.ActivityContext.TraceId);
        Assert.Equal(BaggageValue, serviceResponse.Baggage.GetBaggage(BaggageKey));
    }

    [Fact]
    public async Task TestActorContextPropagation_ShouldExtractActivityContextAndBaggage()
    {
        // Arrange
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .Build();

        var propagator = Propagators.DefaultTextMapPropagator;

        // We have to include the method 'TestContextPropagation' in the interface IMyTestActorService a redirected it to the actor because the normal flow in the base classes is not unit-testable.
        // This still allows us to test what we want to test here, which is the method 'HandleRequestResponseAsync' in TraceContextEnrichedActorServiceV2RemotingDispatcher.
        static ActorBase ActorFactory(ActorService service, ActorId actorId)
        {
            return ((MyTestActorService)service).Actor;
        }

        var actorService = MockActorServiceFactory.CreateCustomActorServiceForActor<MyTestActorService, MyTestActor>(ActorFactory);

        // We create an ActivityContext and Baggage to inject into the request message, instead of starting a new Activity, because the dispatcher is in the same process as the test, and we don't want to set Activity.Current.
        var activityContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var baggage = Baggage.Create(new Dictionary<string, string> { { BaggageKey, BaggageValue } });

        IServiceRemotingRequestMessageHeader actorRemotingMessageHeaders = this.CreateServiceRemotingRequestMessageHeader(typeof(IMyTestActorService), nameof(IMyTestActorService.TestContextPropagation));

        propagator.Inject(new PropagationContext(activityContext, baggage), actorRemotingMessageHeaders, ServiceFabricRemotingUtils.InjectTraceContextIntoServiceRemotingRequestMessageHeader);

        var messageBody = new MockServiceRemotingRequestMessageBody();
        messageBody.SetParameter(0, "valueToReturn", ValueToSend);

        ServiceRemotingRequestMessageMock requestMessage = new(actorRemotingMessageHeaders, messageBody);
        var remotingRequestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        var response = await actorService.Dispatcher.HandleRequestResponseAsync(remotingRequestContext, requestMessage);

        // Assert
        var serviceResponse = (ServiceResponse)response.GetBody().Get(typeof(ServiceResponse));

        Assert.Equal(ValueToSend, serviceResponse.ParameterValue);
        Assert.Equal(activityContext.TraceId, serviceResponse.ActivityContext.TraceId);
        Assert.Equal(BaggageValue, serviceResponse.Baggage.GetBaggage(BaggageKey));
    }

    [Fact]
    public async Task TestServiceRemotingClientContextPropagation_ShouldInjectActivityContextAndBaggage()
    {
        // Arrange
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddSource(ActivitySource.Name)
            .Build();

        // The Baggage set here will be used automatically by TraceContextEnrichedServiceRemotingClientAdapter to inject the baggage into the request message.
        Baggage.SetBaggage(BaggageKey, BaggageValue);

        // The activity is created here will be used automatically by TraceContextEnrichedServiceRemotingClientAdapter to inject the context into the request message.
        using var activity = ActivitySource.StartActivity("TestActivity")!;
        var header = new ServiceRemotingRequestMessageHeaderMock();
        var messageBody = new MockServiceRemotingRequestMessageBody();
        ServiceRemotingRequestMessageMock requestMessage = new(header, messageBody);

        // The ServiceRemotingClientMock reads the headers from the request and injects them into the response, using OpenTelemetry's TextMapPropagator.
        var innerClient = new ServiceRemotingClientMock();
        var serviceRemotingClientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        var response = await serviceRemotingClientAdapter.RequestResponseAsync(requestMessage);

        // Assert
        var responseMessageHeaders = response.GetHeader();
        var propagationContext = Propagators.DefaultTextMapPropagator.Extract(default, responseMessageHeaders, this.ExtractTraceContextFromRequestMessageHeader);

        Assert.Equal(activity.TraceId, propagationContext.ActivityContext.TraceId);
        Assert.Equal(BaggageValue, propagationContext.Baggage.GetBaggage(BaggageKey));
    }

    [Fact]
    public void ServiceRemotingProviderListenerSettings_LoadConfiguredTransportDefaults()
    {
        lock (TransportSettingsLock)
        {
            var originalLoader = TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader;

            try
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = () => new FabricTransportRemotingListenerSettings
                {
                    MaxQueueSize = 35,
                    SecurityCredentials = new X509Credentials(),
                };

                var provider = new TraceContextEnrichedServiceRemotingProviderAttribute
                {
                    MaxMessageSize = 17,
                };

                var actual = provider.GetListenerSettings();

                Assert.Equal(35, actual.MaxQueueSize);
                Assert.Equal(CredentialType.X509, actual.SecurityCredentials.CredentialType);
                Assert.Equal(17, actual.MaxMessageSize);
            }
            finally
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = originalLoader;
            }
        }
    }

    [Fact]
    public void ServiceRemotingProviderListenerSettings_LoadConfiguredRemotingSettings()
    {
        lock (TransportSettingsLock)
        {
            var originalLoader = TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader;

            try
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader = () => new FabricTransportRemotingSettings
                {
                    MaxQueueSize = 35,
                    ConnectTimeout = TimeSpan.FromMilliseconds(7),
                    SecurityCredentials = new X509Credentials(),
                };

                var provider = new TraceContextEnrichedServiceRemotingProviderAttribute
                {
                    MaxMessageSize = 17,
                };

                var actual = provider.GetRemotingSettings();

                Assert.Equal(35, actual.MaxQueueSize);
                Assert.Equal(CredentialType.X509, actual.SecurityCredentials.CredentialType);
                Assert.Equal(TimeSpan.FromMilliseconds(7), actual.ConnectTimeout);
                Assert.Equal(17, actual.MaxMessageSize);
            }
            finally
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.RemotingSettingsLoader = originalLoader;
            }
        }
    }

    [Fact]
    public void ServiceRemotingProviderListenerSettings_RemotingExceptionDepthOverridesConfiguredValue()
    {
        lock (TransportSettingsLock)
        {
            var originalLoader = TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader;

            try
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = () => new FabricTransportRemotingListenerSettings
                {
                    RemotingExceptionDepth = 3,
                };

                var provider = new TraceContextEnrichedServiceRemotingProviderAttribute
                {
                    RemotingExceptionDepth = 5,
                };

                var actual = provider.GetListenerSettings();

                Assert.Equal(5, actual.RemotingExceptionDepth);
            }
            finally
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = originalLoader;
            }
        }
    }

    [Fact]
    public void ServiceRemotingProviderListenerSettings_RemotingExceptionDepthKeepsConfiguredValueWhenNotSet()
    {
        lock (TransportSettingsLock)
        {
            var originalLoader = TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader;

            try
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = () => new FabricTransportRemotingListenerSettings
                {
                    RemotingExceptionDepth = 3,
                };

                var provider = new TraceContextEnrichedServiceRemotingProviderAttribute();

                var actual = provider.GetListenerSettings();

                Assert.Equal(3, actual.RemotingExceptionDepth);
            }
            finally
            {
                TraceContextEnrichedServiceRemotingProviderAttribute.ListenerSettingsLoader = originalLoader;
            }
        }
    }

    [Fact]
    public void ServiceRemotingProvider_ExceptionConvertorsAreNotRegisteredByDefault()
    {
        var provider = new ExceptionConvertorProbeAttribute();

        Assert.Null(provider.GetServiceConvertors());
        Assert.Null(provider.GetClientConvertors());
    }

    [Fact]
    public void ServiceRemotingProvider_DerivedTypeCanRegisterCustomExceptionConvertors()
    {
        var provider = new CustomConvertorProviderAttribute();

        Assert.Single(provider.GetServiceConvertors()!);
        Assert.IsType<CustomTestExceptionConvertorService>(Assert.Single(provider.GetServiceConvertors()!));
        Assert.IsType<CustomTestExceptionConvertorClient>(Assert.Single(provider.GetClientConvertors()!));
    }

    [Fact]
    public void ExceptionConvertors_RoundTripPreservesTheCustomExceptionType()
    {
        var serviceConvertor = new CustomTestExceptionConvertorService();
        var clientConvertor = new CustomTestExceptionConvertorClient();
        var originalException = new CustomTestException("Something failed", "SomeDetail");

        Assert.True(serviceConvertor.TryConvertToServiceException(originalException, out var serviceException));
        Assert.Equal(typeof(CustomTestException).FullName, serviceException.ActualExceptionType);
        Assert.Equal("SomeDetail", serviceException.ActualExceptionData["Detail"]);

        Assert.True(clientConvertor.TryConvertFromServiceException(serviceException, out var actualException));

        var roundTripped = Assert.IsType<CustomTestException>(actualException);
        Assert.Equal("Something failed", roundTripped.Message);
        Assert.Equal("SomeDetail", roundTripped.Detail);
    }

    [Fact]
    public void ExceptionConvertors_UnrelatedExceptionTypesAreNotConverted()
    {
        var serviceConvertor = new CustomTestExceptionConvertorService();
        var clientConvertor = new CustomTestExceptionConvertorClient();

        Assert.False(serviceConvertor.TryConvertToServiceException(new InvalidOperationException("Nope"), out _));

        var unrelated = new ServiceException(typeof(InvalidOperationException).FullName, "Nope");
        Assert.False(clientConvertor.TryConvertFromServiceException(unrelated, out _));
    }

    [Fact]
    public void ActorRemotingProvider_ExceptionConvertorsAreNotRegisteredByDefault()
    {
        var provider = new ActorExceptionConvertorProbeAttribute();

        Assert.Null(provider.GetServiceConvertors());
        Assert.Null(provider.GetClientConvertors());
        Assert.Equal(0, provider.RemotingExceptionDepth);
    }

    [Fact]
    public void ActorRemotingProvider_DerivedTypeCanRegisterCustomExceptionConvertors()
    {
        var provider = new ActorCustomConvertorProviderAttribute
        {
            RemotingExceptionDepth = 7,
        };

        Assert.IsType<CustomTestExceptionConvertorService>(Assert.Single(provider.GetServiceConvertors()!));
        Assert.IsType<CustomTestExceptionConvertorClient>(Assert.Single(provider.GetClientConvertors()!));
        Assert.Equal(7, provider.RemotingExceptionDepth);
    }

    private ServiceRemotingRequestMessageHeaderMock CreateServiceRemotingRequestMessageHeader(Type interfaceType, string methodName)
    {
        var interfaceId = ServiceFabricUtils.GetInterfaceId(interfaceType);

#pragma warning disable IDE0370 // Suppression is unnecessary
        var methodInfo = interfaceType.GetMethod(methodName)!;
        var methodId = ServiceFabricUtils.GetMethodId(methodInfo);
#pragma warning restore IDE0370 // Suppression is unnecessary

        var serviceRemotingRequestMessageHeader = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = interfaceId,
            MethodId = methodId,
        };

        return serviceRemotingRequestMessageHeader;
    }

    private IEnumerable<string> ExtractTraceContextFromRequestMessageHeader(IServiceRemotingResponseMessageHeader responseMessageHeaders, string headerKey)
    {
        if (responseMessageHeaders.TryGetHeaderValue(headerKey, out var headerValueAsBytes))
        {
            var headerValue = Encoding.UTF8.GetString(headerValueAsBytes);

            return [headerValue];
        }

        return [];
    }

    private sealed class ExceptionConvertorProbeAttribute : TraceContextEnrichedServiceRemotingProviderAttribute
    {
        public IEnumerable<RuntimeExceptionConvertor>? GetServiceConvertors() => this.GetServiceExceptionConvertors();

        public IEnumerable<ClientExceptionConvertor>? GetClientConvertors() => this.GetClientExceptionConvertors();
    }

    private sealed class ActorExceptionConvertorProbeAttribute : TraceContextEnrichedActorRemotingProviderAttribute
    {
        public IEnumerable<RuntimeExceptionConvertor>? GetServiceConvertors() => this.GetServiceExceptionConvertors();

        public IEnumerable<ClientExceptionConvertor>? GetClientConvertors() => this.GetClientExceptionConvertors();
    }

    private sealed class ActorCustomConvertorProviderAttribute : TraceContextEnrichedActorRemotingProviderAttribute
    {
        public IEnumerable<RuntimeExceptionConvertor>? GetServiceConvertors() => this.GetServiceExceptionConvertors();

        public IEnumerable<ClientExceptionConvertor>? GetClientConvertors() => this.GetClientExceptionConvertors();

        protected override IEnumerable<RuntimeExceptionConvertor> GetServiceExceptionConvertors() => [new CustomTestExceptionConvertorService()];

        protected override IEnumerable<ClientExceptionConvertor> GetClientExceptionConvertors() => [new CustomTestExceptionConvertorClient()];
    }

    private sealed class CustomConvertorProviderAttribute : TraceContextEnrichedServiceRemotingProviderAttribute
    {
        public IEnumerable<RuntimeExceptionConvertor>? GetServiceConvertors() => this.GetServiceExceptionConvertors();

        public IEnumerable<ClientExceptionConvertor>? GetClientConvertors() => this.GetClientExceptionConvertors();

        protected override IEnumerable<RuntimeExceptionConvertor> GetServiceExceptionConvertors() => [new CustomTestExceptionConvertorService()];

        protected override IEnumerable<ClientExceptionConvertor> GetClientExceptionConvertors() => [new CustomTestExceptionConvertorClient()];
    }

    private sealed class CustomTestException : Exception
    {        public CustomTestException(string message, string detail)
            : base(message)
        {
            this.Detail = detail;
        }

        public CustomTestException(string message, Exception? innerException, string detail)
            : base(message, innerException)
        {
            this.Detail = detail;
        }

        public string Detail { get; }
    }

    private sealed class CustomTestExceptionConvertorService : RuntimeExceptionConvertor
    {
        public Exception[] GetInnerExceptions(Exception exception)
            => exception?.InnerException == null ? [] : [exception.InnerException];

        public bool TryConvertToServiceException(Exception originalException, out ServiceException serviceException)
        {
            if (originalException is CustomTestException customException)
            {
                serviceException = new ServiceException(typeof(CustomTestException).FullName, customException.Message)
                {
                    ActualExceptionStackTrace = customException.StackTrace,
                    ActualExceptionData = new Dictionary<string, string> { ["Detail"] = customException.Detail },
                };

                return true;
            }

            serviceException = null!;

            return false;
        }
    }

    private sealed class CustomTestExceptionConvertorClient : ClientExceptionConvertor
    {
        public bool TryConvertFromServiceException(ServiceException serviceException, out Exception actualException)
            => this.TryConvertFromServiceException(serviceException, (Exception)null!, out actualException);

        public bool TryConvertFromServiceException(ServiceException serviceException, Exception innerException, out Exception actualException)
        {
            var expectedExceptionType = typeof(CustomTestException).FullName!;

            if (serviceException?.ActualExceptionType == expectedExceptionType)
            {
                actualException = new CustomTestException(serviceException.Message, innerException, serviceException.ActualExceptionData["Detail"]);

                return true;
            }

            actualException = null!;

            return false;
        }

        public bool TryConvertFromServiceException(ServiceException serviceException, Exception[] innerExceptions, out Exception actualException)
            => this.TryConvertFromServiceException(serviceException, innerExceptions?.Length > 0 ? innerExceptions[0] : (Exception)null!, out actualException);
    }
}
