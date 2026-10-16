// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using ServiceFabric.Mocks.RemotingV2;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

[Collection(ServiceFabricRemotingCollectionDefinition.Name)]
public class ServiceRemotingMessageDispatcherAdapterTests
{
    private const string MethodName = "MyMethod";

    [Fact]
    public void Constructor_ThrowsWhenTheDispatcherIsNull()
        => Assert.Throws<ArgumentNullException>(() => new ServiceRemotingMessageDispatcherAdapter(dispatcher: null!));

    [Fact]
    public void GetRemotingMessageBodyFactory_ReturnsTheInnerDispatcherMessageBodyFactory()
    {
        var messageBodyFactory = new MockServiceRemotingMessageBodyFactory();
        var innerDispatcher = new ServiceRemotingMessageHandlerMock(messageBodyFactory);
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);

        Assert.Same(messageBodyFactory, dispatcherAdapter.GetRemotingMessageBodyFactory());
    }

    [Fact]
    public void HandleOneWayMessage_IsForwardedToTheInnerDispatcher()
    {
        var innerDispatcher = CreateInnerDispatcher();
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        var requestMessage = CreateRequestMessage(MethodName);

        dispatcherAdapter.HandleOneWayMessage(requestMessage);

        Assert.Same(requestMessage, innerDispatcher.LastOneWayMessage);
    }

    [Fact]
    public async Task HandleRequestResponseAsync_ThrowsWhenTheRequestIsNull()
    {
        var innerDispatcher = CreateInnerDispatcher();
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        var requestContext = new FabricTransportServiceRemotingRequestContextMock();

        await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage: null!));
    }

    [Fact]
    public async Task HandleRequestResponseAsync_FilteredOutRequestsAreNotTraced()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options => options.Filter = _ => false);
        var expectedResponse = new MockServiceRemotingResponseMessage();
        var innerDispatcher = CreateInnerDispatcher();
        innerDispatcher.ResponseMessage = expectedResponse;
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        var requestContext = new FabricTransportServiceRemotingRequestContextMock();
        var requestMessage = CreateRequestMessage(MethodName);

        var response = await dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage);

        Assert.Same(expectedResponse, response);
        Assert.Empty(exportedActivities);
    }

    [Fact]
    public async Task HandleRequestResponseAsync_UsesDefaultActivityNameWhenMethodNameIsMissing()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options => { });
        var innerDispatcher = CreateInnerDispatcher();
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        var requestContext = new FabricTransportServiceRemotingRequestContextMock();
        var requestMessage = CreateRequestMessage(methodName: null);

        await dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage);

        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ServiceFabricRemotingActivitySource.IncomingRequestActivityName, activity.DisplayName);
        Assert.Equal(ActivityKind.Server, activity.Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleRequestResponseAsync_RecordsFailedRequests(bool addExceptionAtServer)
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options => options.AddExceptionAtServer = addExceptionAtServer);
        var exception = new InvalidOperationException("Request failed");
        var innerDispatcher = CreateInnerDispatcher();
        innerDispatcher.Exception = exception;
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);
        var requestContext = new FabricTransportServiceRemotingRequestContextMock();
        var requestMessage = CreateRequestMessage(MethodName);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcherAdapter.HandleRequestResponseAsync(requestContext, requestMessage));

        Assert.Same(exception, actual);
        var activity = Assert.Single(exportedActivities);
        Assert.Equal(MethodName, activity.DisplayName);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(addExceptionAtServer, activity.Events.Any(activityEvent => activityEvent.Name == "exception"));
    }

    [Fact]
    public void Dispose_DisposesTheInnerDispatcherWhenItIsDisposable()
    {
        var messageBodyFactory = new MockServiceRemotingMessageBodyFactory();
        var innerDispatcher = new DisposableServiceRemotingMessageHandlerMock(messageBodyFactory);
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);

        dispatcherAdapter.Dispose();

        Assert.True(innerDispatcher.IsDisposed);
    }

    [Fact]
    public void Dispose_IgnoresInnerDispatchersThatAreNotDisposable()
    {
        var innerDispatcher = CreateInnerDispatcher();
        var dispatcherAdapter = new ServiceRemotingMessageDispatcherAdapter(innerDispatcher);

        var exception = Record.Exception(dispatcherAdapter.Dispose);

        Assert.Null(exception);
    }

    private static TracerProvider CreateTracerProvider(List<Activity> exportedActivities, Action<ServiceFabricRemotingInstrumentationOptions> configure)
        => Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation(configure)
            .AddInMemoryExporter(exportedActivities)
            .Build();

    private static ServiceRemotingMessageHandlerMock CreateInnerDispatcher()
    {
        var messageBodyFactory = new MockServiceRemotingMessageBodyFactory();
        var responseMessage = new MockServiceRemotingResponseMessage();

        return new ServiceRemotingMessageHandlerMock(messageBodyFactory) { ResponseMessage = responseMessage };
    }

    private static ServiceRemotingRequestMessageMock CreateRequestMessage(string? methodName)
    {
        var header = new ServiceRemotingRequestMessageHeaderMock { MethodName = methodName };
        var messageBody = new MockServiceRemotingRequestMessageBody();

        return new ServiceRemotingRequestMessageMock(header, messageBody);
    }
}
