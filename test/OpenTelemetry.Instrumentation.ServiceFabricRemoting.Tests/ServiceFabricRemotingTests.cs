// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Fabric;
using System.Reflection;
using System.Text;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests.Mocks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.RemotingV2;
using Xunit;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class ServiceFabricRemotingTests
{
    private const string ValueToSend = "SomeValue";
    private const string BaggageKey = "SomeBaggageKey";
    private const string BaggageValue = "SomeBaggageValue";

    [Fact]
    public async Task TestStatefulServiceContextPropagation_ShouldExtractActivityContextAndBaggage()
    {
        // Arrange
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .Build();

        TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;

        StatefulServiceContext serviceContext = MockStatefulServiceContextFactory.Default;
        MockReliableStateManager reliableStateManager = new MockReliableStateManager();

        // We need to create the service, then the dispatcher, and then set the dispatcher on the service, because the dispatcher needs the service as an argument, and the service needs the dispatcher.
        MyTestStatefulService myStatefulService = new MyTestStatefulService(serviceContext, reliableStateManager);
        TraceContextEnrichedServiceV2RemotingDispatcher dispatcher = new TraceContextEnrichedServiceV2RemotingDispatcher(serviceContext, myStatefulService);
        myStatefulService.SetDispatcher(dispatcher);

        // We create an ActivityContext and Baggage to inject into the request message, instead of starting a new Activity, because the dispatcher is in the same process as the test, and we don't want to set Activity.Current.
        ActivityContext activityContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        Baggage baggage = Baggage.Create(new Dictionary<string, string> { { BaggageKey, BaggageValue } });

        IServiceRemotingRequestMessageHeader remotingRequestMessageHeader = this.CreateServiceRemotingRequestMessageHeader(typeof(ITestMyStatefulService), nameof(ITestMyStatefulService.TestContextPropagation));

        propagator.Inject(new PropagationContext(activityContext, baggage), remotingRequestMessageHeader, this.InjectTraceContextIntoServiceRemotingRequestMessageHeader);

        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        messageBody.SetParameter(0, "valueToReturn", ValueToSend);

        ServiceRemotingRequestMessageMock requestMessage = new(remotingRequestMessageHeader, messageBody);
        FabricTransportServiceRemotingRequestContextMock remotingRequestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        IServiceRemotingResponseMessage response = await dispatcher.HandleRequestResponseAsync(remotingRequestContext, requestMessage);

        // Assert
        ServiceResponse serviceResponse = (ServiceResponse)response.GetBody().Get(typeof(ServiceResponse));

        Assert.Equal(ValueToSend, serviceResponse.ParameterValue);
        Assert.Equal(activityContext.TraceId, serviceResponse.ActivityContext.TraceId);
        Assert.Equal(BaggageValue, serviceResponse.Baggage.GetBaggage(BaggageKey));
    }

    [Fact]
    public async Task TestActorContextPropagation_ShouldExtractActivityContextAndBaggage()
    {
        // Arrange
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .Build();

        TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;

        // We have to include the method 'TestContextPropagation' in the interface IMyTestActorService a redirected it to the actor because the normal flow in the base classes is not unit-testable.
        // This still allows us to test what we want to test here, which is the method 'HandleRequestResponseAsync' in TraceContextEnrichedActorServiceV2RemotingDispatcher.
        Func<ActorService, ActorId, ActorBase> actorFactory = (service, actorId) => ((MyTestActorService)service).Actor;
        MyTestActorService actorService = MockActorServiceFactory.CreateCustomActorServiceForActor<MyTestActorService, MyTestActor>(actorFactory);

        // We create an ActivityContext and Baggage to inject into the request message, instead of starting a new Activity, because the dispatcher is in the same process as the test, and we don't want to set Activity.Current.
        ActivityContext activityContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        Baggage baggage = Baggage.Create(new Dictionary<string, string> { { BaggageKey, BaggageValue } });

        IServiceRemotingRequestMessageHeader actorRemotingMessageHeaders = this.CreateServiceRemotingRequestMessageHeader(typeof(IMyTestActorService), nameof(IMyTestActorService.TestContextPropagation));

        propagator.Inject(new PropagationContext(activityContext, baggage), actorRemotingMessageHeaders, this.InjectTraceContextIntoServiceRemotingRequestMessageHeader);

        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        messageBody.SetParameter(0, "valueToReturn", ValueToSend);

        ServiceRemotingRequestMessageMock requestMessage = new(actorRemotingMessageHeaders, messageBody);
        FabricTransportServiceRemotingRequestContextMock remotingRequestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        IServiceRemotingResponseMessage response = await actorService.Dispatcher.HandleRequestResponseAsync(remotingRequestContext, requestMessage);

        // Assert
        ServiceResponse serviceResponse = (ServiceResponse)response.GetBody().Get(typeof(ServiceResponse));

        Assert.Equal(ValueToSend, serviceResponse.ParameterValue);
        Assert.Equal(activityContext.TraceId, serviceResponse.ActivityContext.TraceId);
        Assert.Equal(BaggageValue, serviceResponse.Baggage.GetBaggage(BaggageKey));
    }

    private ServiceRemotingRequestMessageHeaderMock CreateServiceRemotingRequestMessageHeader(Type interfaceType, string methodName)
    {
        int interfaceId = ServiceFabricUtils.GetInterfaceId(interfaceType);

        MethodInfo methodInfo = interfaceType.GetMethod(methodName)!;
        int methodId = ServiceFabricUtils.GetMethodId(methodInfo);

        ServiceRemotingRequestMessageHeaderMock serviceRemotingRequestMessageHeader = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = interfaceId,
            MethodId = methodId,
        };

        return serviceRemotingRequestMessageHeader;
    }

    private void InjectTraceContextIntoServiceRemotingRequestMessageHeader(IServiceRemotingRequestMessageHeader requestMessageHeader, string key, string value)
    {
        if (!requestMessageHeader.TryGetHeaderValue(key, out byte[] _))
        {
            byte[] valueAsBytes = Encoding.UTF8.GetBytes(value);

            requestMessageHeader.AddHeader(key, valueAsBytes);
        }
    }
}
