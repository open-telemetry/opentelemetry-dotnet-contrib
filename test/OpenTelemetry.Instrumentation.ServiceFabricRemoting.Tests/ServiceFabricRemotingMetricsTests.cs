// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Fabric;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.RemotingV2;
using Xunit;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

public class ServiceFabricRemotingMetricsTests
{
    private const string ExpectedRpcSystemName = "service_fabric_remoting";

    [Fact]
    public async Task ClientRequestResponseAsync_EmitsRpcClientCallDuration()
    {
        // Arrange
        List<Metric> exportedMetrics = new List<Metric>();
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        ServiceRemotingRequestMessageHeaderMock header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "TestMethod",
        };
        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        ServiceRemotingRequestMessageMock requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        ServiceRemotingClientMock innerClient = new ServiceRemotingClientMock();
        TraceContextEnrichedServiceRemotingClientAdapter clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        await clientAdapter.RequestResponseAsync(requestMessage);
        meterProvider.ForceFlush();

        // Assert
        Metric metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.client.call.duration");
        Assert.Equal(MetricType.Histogram, metric.MetricType);

        (Dictionary<string, object?> tags, double sum, long count) = FindHistogramPointForMethod(metric, "TestMethod");
        Assert.Equal(ExpectedRpcSystemName, tags["rpc.system.name"]);
        Assert.DoesNotContain("error.type", tags.Keys);
        Assert.Equal(1, count);
        Assert.True(sum > 0, $"Expected recorded duration to be > 0 seconds but was {sum}.");
        Assert.True(sum < 5, $"Expected recorded duration to be < 5 seconds (unit sanity check) but was {sum}.");
    }

    [Fact]
    public async Task ServerHandleRequestResponseAsync_EmitsRpcServerCallDuration()
    {
        // Arrange
        List<Metric> exportedMetrics = new List<Metric>();
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        // The test service's method dereferences Activity.Current, so we also need a TracerProvider
        // so the dispatcher adapter actually creates an Activity.
        using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .Build();

        StatefulServiceContext serviceContext = MockStatefulServiceContextFactory.Default;
        MockReliableStateManager reliableStateManager = new MockReliableStateManager();
        MyTestStatefulService statefulService = new MyTestStatefulService(serviceContext, reliableStateManager);
        ServiceRemotingMessageDispatcher innerDispatcher = new ServiceRemotingMessageDispatcher(serviceContext, statefulService);
        ServiceRemotingMessageDispatcherAdapter dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        statefulService.SetDispatcher(dispatcherAdapter);

        ServiceRemotingRequestMessageHeaderMock header = this.CreateHeaderFor(typeof(ITestMyStatefulService), nameof(ITestMyStatefulService.TestContextPropagation));
        header.MethodName = nameof(ITestMyStatefulService.TestContextPropagation);
        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        messageBody.SetParameter(0, "valueToReturn", "SomeValue");
        ServiceRemotingRequestMessageMock requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);
        FabricTransportServiceRemotingRequestContextMock requestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        await dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage);
        meterProvider.ForceFlush();

        // Assert
        Metric metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.server.call.duration");
        Assert.Equal(MetricType.Histogram, metric.MetricType);

        (Dictionary<string, object?> tags, double sum, long count) = FindHistogramPointForMethod(metric, nameof(ITestMyStatefulService.TestContextPropagation));
        Assert.Equal(ExpectedRpcSystemName, tags["rpc.system.name"]);
        Assert.DoesNotContain("error.type", tags.Keys);
        Assert.Equal(1, count);
        Assert.True(sum > 0, $"Expected recorded duration to be > 0 seconds but was {sum}.");
        Assert.True(sum < 5, $"Expected recorded duration to be < 5 seconds (unit sanity check) but was {sum}.");
    }

    [Fact]
    public async Task ClientRequestResponseAsync_OnException_EmitsMetricWithErrorType()
    {
        // Arrange
        List<Metric> exportedMetrics = new List<Metric>();
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        ServiceRemotingRequestMessageHeaderMock header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "FailingClientMethod",
        };
        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        ServiceRemotingRequestMessageMock requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        InvalidOperationException expectedException = new InvalidOperationException("simulated client failure");
        ThrowingServiceRemotingClient innerClient = new ThrowingServiceRemotingClient { ExceptionToThrow = expectedException };
        TraceContextEnrichedServiceRemotingClientAdapter clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => clientAdapter.RequestResponseAsync(requestMessage));
        Assert.Same(expectedException, actual);
        meterProvider.ForceFlush();

        // Assert
        Metric metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.client.call.duration");
        (Dictionary<string, object?> tags, double sum, long count) = FindHistogramPointForMethod(metric, "FailingClientMethod");
        Assert.Equal(ExpectedRpcSystemName, tags["rpc.system.name"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["error.type"]);
        Assert.Equal(1, count);
        Assert.True(sum > 0, $"Expected recorded duration to be > 0 seconds but was {sum}.");
        Assert.True(sum < 5, $"Expected recorded duration to be < 5 seconds (unit sanity check) but was {sum}.");
    }

    [Fact]
    public async Task ServerHandleRequestResponseAsync_OnException_EmitsMetricWithErrorType()
    {
        // Arrange
        List<Metric> exportedMetrics = new List<Metric>();
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        InvalidOperationException expectedException = new InvalidOperationException("simulated server failure");
        ThrowingServiceRemotingMessageHandler innerDispatcher = new ThrowingServiceRemotingMessageHandler { ExceptionToThrow = expectedException };
        ServiceRemotingMessageDispatcherAdapter dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);

        ServiceRemotingRequestMessageHeaderMock header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "FailingServerMethod",
        };
        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        ServiceRemotingRequestMessageMock requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);
        FabricTransportServiceRemotingRequestContextMock requestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage));
        Assert.Same(expectedException, actual);
        meterProvider.ForceFlush();

        // Assert
        Metric metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.server.call.duration");
        (Dictionary<string, object?> tags, double sum, long count) = FindHistogramPointForMethod(metric, "FailingServerMethod");
        Assert.Equal(ExpectedRpcSystemName, tags["rpc.system.name"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["error.type"]);
        Assert.Equal(1, count);
        Assert.True(sum > 0, $"Expected recorded duration to be > 0 seconds but was {sum}.");
        Assert.True(sum < 5, $"Expected recorded duration to be < 5 seconds (unit sanity check) but was {sum}.");
    }

    [Fact]
    public async Task Filter_ReturnsFalse_NoMetricRecorded()
    {
        // Arrange
        List<Metric> exportedMetrics = new List<Metric>();
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation(options => options.Filter = _ => false)
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        ServiceRemotingRequestMessageHeaderMock header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "TestMethod",
        };
        MockServiceRemotingRequestMessageBody messageBody = new MockServiceRemotingRequestMessageBody();
        ServiceRemotingRequestMessageMock requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        ServiceRemotingClientMock innerClient = new ServiceRemotingClientMock();
        TraceContextEnrichedServiceRemotingClientAdapter clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        await clientAdapter.RequestResponseAsync(requestMessage);
        meterProvider.ForceFlush();

        // Assert
        Assert.DoesNotContain(exportedMetrics, m => m.Name == "rpc.client.call.duration");
    }

    // Finds the histogram point tagged with the given rpc.method value. Filtering by tag
    // keeps tests robust against cross-test contamination from the shared static Meter
    // when running test classes in parallel.
    private static (Dictionary<string, object?> Tags, double Sum, long Count) FindHistogramPointForMethod(Metric metric, string expectedMethod)
    {
        foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
        {
            Dictionary<string, object?> tags = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> tag in point.Tags)
            {
                tags[tag.Key] = tag.Value;
            }

            if (tags.TryGetValue("rpc.method", out object? method) && string.Equals(method as string, expectedMethod, StringComparison.Ordinal))
            {
                return (tags, point.GetHistogramSum(), point.GetHistogramCount());
            }
        }

        throw new Xunit.Sdk.XunitException($"No metric point found with rpc.method='{expectedMethod}'");
    }

    private ServiceRemotingRequestMessageHeaderMock CreateHeaderFor(Type interfaceType, string methodName)
    {
        int interfaceId = ServiceFabricUtils.GetInterfaceId(interfaceType);
#pragma warning disable IDE0370 // Suppression is unnecessary
        System.Reflection.MethodInfo methodInfo = interfaceType.GetMethod(methodName)!;
        int methodId = ServiceFabricUtils.GetMethodId(methodInfo);
#pragma warning restore IDE0370 // Suppression is unnecessary

        return new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = interfaceId,
            MethodId = methodId,
        };
    }

    private sealed class ThrowingServiceRemotingClient : IServiceRemotingClient
    {
        public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("simulated failure");

        public ResolvedServicePartition ResolvedServicePartition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string ListenerName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ResolvedServiceEndpoint Endpoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => throw this.ExceptionToThrow;

        public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();
    }

    private sealed class ThrowingServiceRemotingMessageHandler : IServiceRemotingMessageHandler
    {
        public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("simulated failure");

        public IServiceRemotingMessageBodyFactory GetRemotingMessageBodyFactory() => throw new NotImplementedException();

        public void HandleOneWayMessage(IServiceRemotingRequestMessage requestMessage) => throw new NotImplementedException();

        public Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(IServiceRemotingRequestContext requestContext, IServiceRemotingRequestMessage requestMessage) => throw this.ExceptionToThrow;
    }
}
