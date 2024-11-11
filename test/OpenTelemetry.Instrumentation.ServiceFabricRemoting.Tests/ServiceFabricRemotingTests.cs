// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Fabric;
using System.Reflection;
using System.Text;
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

    private static readonly ActivitySource TestsActivitySource = new ActivitySource(nameof(ServiceFabricRemotingTests));

    [Fact]
    public async Task TestStatefulServiceContextPropagation_ShouldExtractActivityContextAndBaggage()
    {
        // Arrange
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddSource(TestsActivitySource.Name)
            .Build();

        TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;

        StatefulServiceContext serviceContext = MockStatefulServiceContextFactory.Default;
        MockReliableStateManager reliableStateManager = new MockReliableStateManager();

        // We need to create the service, then the dispatcher, and then set the dispatcher on the service, because the dispatcher needs the service as an argument.
        MyStatefulService myStatefulService = new MyStatefulService(serviceContext, reliableStateManager);
        TraceContextEnrichedServiceV2RemotingDispatcher dispatcher = new TraceContextEnrichedServiceV2RemotingDispatcher(serviceContext, myStatefulService);
        myStatefulService.SetDispatcher(dispatcher);

        // We create an ActivityContext and Baggage to inject into the request message, instead of starting a new Activity, because the dispatcher is in the same process as the test.
        ActivityContext activityContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        Baggage baggage = Baggage.Create(new Dictionary<string, string> { { BaggageKey, BaggageValue } });

        IServiceRemotingRequestMessageHeader remotingRequestMessageHeader = this.CreateServiceRemotingRequestMessageHeader(typeof(IMyStatefulService), nameof(IMyStatefulService.TestContextPropagation));

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
