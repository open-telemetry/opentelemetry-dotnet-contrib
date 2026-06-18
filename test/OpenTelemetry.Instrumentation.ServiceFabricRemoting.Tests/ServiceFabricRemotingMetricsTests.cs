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

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

// Builds TracerProviders, so it shares the same collection as ServiceFabricRemotingTests to avoid
// concurrent TracerProvider churn on the instrumentation's process-wide static ActivitySource.
[Collection("TracerProviderDependent")]
public class ServiceFabricRemotingMetricsTests
{
    private const string ExpectedRpcSystemName = "service_fabric_remoting";

    [Fact]
    public async Task ClientRequestResponseAsync_EmitsRpcClientCallDuration()
    {
        // Arrange
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "TestMethod",
        };
        var messageBody = new MockServiceRemotingRequestMessageBody();
        var requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        var innerClient = new ServiceRemotingClientMock();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        await clientAdapter.RequestResponseAsync(requestMessage);
        meterProvider.ForceFlush();

        // Assert
        var metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.client.call.duration");
        Assert.Equal(MetricType.Histogram, metric.MetricType);

        (var tags, var sum, var count) = FindHistogramPointForMethod(metric, "TestMethod");
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
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        // The test service's method dereferences Activity.Current, so we also need a TracerProvider
        // so the dispatcher adapter actually creates an Activity.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .Build();

        var serviceContext = MockStatefulServiceContextFactory.Default;
        var reliableStateManager = new MockReliableStateManager();
        var statefulService = new MyTestStatefulService(serviceContext, reliableStateManager);
        var innerDispatcher = new ServiceRemotingMessageDispatcher(serviceContext, statefulService);
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        statefulService.SetDispatcher(dispatcherAdapter);

        var header = this.CreateHeaderFor(typeof(ITestMyStatefulService), nameof(ITestMyStatefulService.TestContextPropagation));
        header.MethodName = nameof(ITestMyStatefulService.TestContextPropagation);
        var messageBody = new MockServiceRemotingRequestMessageBody();
        messageBody.SetParameter(0, "valueToReturn", "SomeValue");
        var requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);
        var requestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        await dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage);
        meterProvider.ForceFlush();

        // Assert
        var metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.server.call.duration");
        Assert.Equal(MetricType.Histogram, metric.MetricType);

        (var tags, var sum, var count) = FindHistogramPointForMethod(metric, nameof(ITestMyStatefulService.TestContextPropagation));
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
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "FailingClientMethod",
        };
        var messageBody = new MockServiceRemotingRequestMessageBody();
        var requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        var expectedException = new InvalidOperationException("simulated client failure");
        var innerClient = new ThrowingServiceRemotingClient { ExceptionToThrow = expectedException };
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => clientAdapter.RequestResponseAsync(requestMessage));
        Assert.Same(expectedException, actual);
        meterProvider.ForceFlush();

        // Assert
        var metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.client.call.duration");
        var (tags, sum, count) = FindHistogramPointForMethod(metric, "FailingClientMethod");
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
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        var expectedException = new InvalidOperationException("simulated server failure");
        var innerDispatcher = new ThrowingServiceRemotingMessageHandler { ExceptionToThrow = expectedException };
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);

        var header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "FailingServerMethod",
        };
        var messageBody = new MockServiceRemotingRequestMessageBody();
        var requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);
        var requestContext = new FabricTransportServiceRemotingRequestContextMock();

        // Act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage));
        Assert.Same(expectedException, actual);
        meterProvider.ForceFlush();

        // Assert
        var metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.server.call.duration");
        (var tags, var sum, var count) = FindHistogramPointForMethod(metric, "FailingServerMethod");
        Assert.Equal(ExpectedRpcSystemName, tags["rpc.system.name"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["error.type"]);
        Assert.Equal(1, count);
        Assert.True(sum > 0, $"Expected recorded duration to be > 0 seconds but was {sum}.");
        Assert.True(sum < 5, $"Expected recorded duration to be < 5 seconds (unit sanity check) but was {sum}.");
    }

    [Fact]
    public async Task Filter_ReturnsFalse_MetricStillRecorded()
    {
        // Filter is a tracing-only option. Even when it rejects a request (so no activity is
        // created), the RPC duration metric must still be recorded so rate / error-rate
        // dashboards reflect all real traffic.

        // Arrange
        var exportedMetrics = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddServiceFabricRemotingInstrumentation()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation(options => options.Filter = _ => false)
            .Build();

        var header = new ServiceRemotingRequestMessageHeaderMock
        {
            InterfaceId = 1,
            MethodId = 1,
            MethodName = "FilteredMethod",
        };
        var messageBody = new MockServiceRemotingRequestMessageBody();
        var requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        var innerClient = new ServiceRemotingClientMock();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        // Act
        await clientAdapter.RequestResponseAsync(requestMessage);
        meterProvider.ForceFlush();

        // Assert
        var metric = Assert.Single(exportedMetrics, m => m.Name == "rpc.client.call.duration");
        (var tags, _, var count) = FindHistogramPointForMethod(metric, "FilteredMethod");
        Assert.Equal(ExpectedRpcSystemName, tags["rpc.system.name"]);
        Assert.Equal(1, count);
    }

    // Finds the histogram point tagged with the given rpc.method value. Filtering by tag
    // keeps tests robust against cross-test contamination from the shared static Meter
    // when running test classes in parallel.
    private static (Dictionary<string, object?> Tags, double Sum, long Count) FindHistogramPointForMethod(Metric metric, string expectedMethod)
    {
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            var tags = new Dictionary<string, object?>();
            foreach (var tag in point.Tags)
            {
                tags[tag.Key] = tag.Value;
            }

            if (tags.TryGetValue("rpc.method", out var method) && string.Equals(method as string, expectedMethod, StringComparison.Ordinal))
            {
                return (tags, point.GetHistogramSum(), point.GetHistogramCount());
            }
        }

        throw new Xunit.Sdk.XunitException($"No metric point found with rpc.method='{expectedMethod}'");
    }

    private ServiceRemotingRequestMessageHeaderMock CreateHeaderFor(Type interfaceType, string methodName)
    {
        var interfaceId = ServiceFabricUtils.GetInterfaceId(interfaceType);
#pragma warning disable IDE0370 // Suppression is unnecessary
        var methodInfo = interfaceType.GetMethod(methodName)!;
        var methodId = ServiceFabricUtils.GetMethodId(methodInfo);
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
