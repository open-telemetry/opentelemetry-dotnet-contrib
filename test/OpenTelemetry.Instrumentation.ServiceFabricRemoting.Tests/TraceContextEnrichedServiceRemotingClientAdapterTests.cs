// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using OpenTelemetry.Trace;
using ServiceFabric.Mocks;
using ServiceFabric.Mocks.RemotingV2;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

[Collection(ServiceFabricRemotingCollectionDefinition.Name)]
public class TraceContextEnrichedServiceRemotingClientAdapterTests
{
    private const string MethodName = "MyMethod";
    private const string TraceParentHeaderName = "traceparent";

    [Fact]
    public void Properties_AreForwardedToTheInnerClient()
    {
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        clientAdapter.ListenerName = "MyListener";
        clientAdapter.ResolvedServicePartition = null!;
        clientAdapter.Endpoint = null!;

        Assert.Equal("MyListener", innerClient.ListenerName);
        Assert.Equal("MyListener", clientAdapter.ListenerName);
        Assert.Null(clientAdapter.ResolvedServicePartition);
        Assert.Null(clientAdapter.Endpoint);
    }

    [Fact]
    public void SendOneWay_IsForwardedToTheInnerClient()
    {
        var exception = new InvalidOperationException("One way");
        var innerClient = new ThrowingServiceRemotingClientMock(exception);
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(MethodName);

        var actual = Assert.Throws<InvalidOperationException>(() => clientAdapter.SendOneWay(requestMessage));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task RequestResponseAsync_ThrowsWhenTheRequestIsNull()
    {
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);

        await Assert.ThrowsAsync<ArgumentNullException>(() => clientAdapter.RequestResponseAsync(requestRequestMessage: null!));
    }

    [Fact]
    public async Task RequestResponseAsync_FilteredOutRequestsAreNotTraced()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options => options.Filter = _ => false);
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(MethodName);

        var response = await clientAdapter.RequestResponseAsync(requestMessage);

        Assert.Same(innerClient.ServiceRemotingResponseMessage, response);
        Assert.Empty(exportedActivities);
        Assert.False(requestMessage.GetHeader().TryGetHeaderValue(TraceParentHeaderName, out _));
    }

    [Fact]
    public async Task RequestResponseAsync_CreatesClientActivityAndEnrichesIt()
    {
        var exportedActivities = new List<Activity>();
        IServiceRemotingRequestMessage? enrichedRequest = null;
        IServiceRemotingResponseMessage? enrichedResponse = null;
        Exception? enrichedException = null;
        using var provider = CreateTracerProvider(exportedActivities, options =>
        {
            options.EnrichAtClientFromRequest = (activity, request) => enrichedRequest = request;
            options.EnrichAtClientFromResponse = (activity, response, exception) =>
            {
                enrichedResponse = response;
                enrichedException = exception;
            };
        });
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(MethodName);

        var response = await clientAdapter.RequestResponseAsync(requestMessage);

        var activity = Assert.Single(exportedActivities);
        Assert.Equal(MethodName, activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Same(requestMessage, enrichedRequest);
        Assert.Same(response, enrichedResponse);
        Assert.Null(enrichedException);
        Assert.True(requestMessage.GetHeader().TryGetHeaderValue(TraceParentHeaderName, out _));
    }

    [Fact]
    public async Task RequestResponseAsync_UsesDefaultActivityNameWhenMethodNameIsMissing()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options => { });
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(methodName: null);

        await clientAdapter.RequestResponseAsync(requestMessage);

        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ServiceFabricRemotingActivitySource.OutgoingRequestActivityName, activity.DisplayName);
    }

    [Fact]
    public async Task RequestResponseAsync_EnrichmentFailuresDoNotFailTheRequest()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options =>
        {
            options.EnrichAtClientFromRequest = (activity, request) => throw new InvalidOperationException("Request enrichment");
            options.EnrichAtClientFromResponse = (activity, response, exception) => throw new InvalidOperationException("Response enrichment");
        });
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(MethodName);

        var response = await clientAdapter.RequestResponseAsync(requestMessage);

        Assert.Same(innerClient.ServiceRemotingResponseMessage, response);
        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public async Task RequestResponseAsync_SetsErrorStatusWhenTheContextCannotBeInjected()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options => { });
        var innerClient = CreateInnerClient();
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var header = new ThrowingServiceRemotingRequestMessageHeaderMock { MethodName = MethodName };
        var messageBody = new MockServiceRemotingRequestMessageBody();
        var requestMessage = new ServiceRemotingRequestMessageMock(header, messageBody);

        await clientAdapter.RequestResponseAsync(requestMessage);

        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains(ThrowingServiceRemotingRequestMessageHeaderMock.ErrorMessage, activity.StatusDescription, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestResponseAsync_RecordsFailedRequests(bool addExceptionAtClient)
    {
        var exportedActivities = new List<Activity>();
        IServiceRemotingResponseMessage? enrichedResponse = null;
        Exception? enrichedException = null;
        using var provider = CreateTracerProvider(exportedActivities, options =>
        {
            options.AddExceptionAtClient = addExceptionAtClient;
            options.EnrichAtClientFromResponse = (activity, response, exception) =>
            {
                enrichedResponse = response;
                enrichedException = exception;
            };
        });
        var exception = new InvalidOperationException("Request failed");
        var innerClient = new ThrowingServiceRemotingClientMock(exception);
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(MethodName);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => clientAdapter.RequestResponseAsync(requestMessage));

        Assert.Same(exception, actual);
        var activity = Assert.Single(exportedActivities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(addExceptionAtClient, activity.Events.Any(activityEvent => activityEvent.Name == "exception"));
        Assert.Null(enrichedResponse);
        Assert.Same(exception, enrichedException);
    }

    [Fact]
    public async Task RequestResponseAsync_ResponseEnrichmentFailuresDoNotHideTheOriginalException()
    {
        var exportedActivities = new List<Activity>();
        using var provider = CreateTracerProvider(exportedActivities, options =>
            options.EnrichAtClientFromResponse = (activity, response, exception) => throw new NotSupportedException("Response enrichment"));
        var exception = new InvalidOperationException("Request failed");
        var innerClient = new ThrowingServiceRemotingClientMock(exception);
        var clientAdapter = new TraceContextEnrichedServiceRemotingClientAdapter(innerClient);
        var requestMessage = CreateRequestMessage(MethodName);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => clientAdapter.RequestResponseAsync(requestMessage));

        Assert.Same(exception, actual);
    }

    private static TracerProvider CreateTracerProvider(List<Activity> exportedActivities, Action<ServiceFabricRemotingInstrumentationOptions> configure)
        => Sdk.CreateTracerProviderBuilder()
            .AddServiceFabricRemotingInstrumentation(configure)
            .AddInMemoryExporter(exportedActivities)
            .Build();

    private static MockActorServiceRemotingClient CreateInnerClient()
    {
        var actorService = MockActorServiceFactory.CreateActorServiceForActor<MyTestActor>();
        var responseHeader = new ServiceRemotingResponseMessageHeaderMock();
        var responseMessage = new MockServiceRemotingResponseMessage { Header = responseHeader };

        return new MockActorServiceRemotingClient(actorService) { ServiceRemotingResponseMessage = responseMessage };
    }

    private static ServiceRemotingRequestMessageMock CreateRequestMessage(string? methodName)
    {
        var header = new ServiceRemotingRequestMessageHeaderMock { MethodName = methodName };
        var messageBody = new MockServiceRemotingRequestMessageBody();

        return new ServiceRemotingRequestMessageMock(header, messageBody);
    }
}
