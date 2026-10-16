// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

internal class ServiceRemotingMessageHandlerMock : IServiceRemotingMessageHandler
{
    private readonly Func<IServiceRemotingRequestMessage, Task<IServiceRemotingResponseMessage>> requestResponseHandler;
    private readonly IServiceRemotingMessageBodyFactory messageBodyFactory;

    public ServiceRemotingMessageHandlerMock(Func<IServiceRemotingRequestMessage, Task<IServiceRemotingResponseMessage>> requestResponseHandler, IServiceRemotingMessageBodyFactory messageBodyFactory)
    {
        this.requestResponseHandler = requestResponseHandler;
        this.messageBodyFactory = messageBodyFactory;
    }

    public IServiceRemotingRequestMessage? LastOneWayMessage { get; private set; }

    public IServiceRemotingMessageBodyFactory GetRemotingMessageBodyFactory() => this.messageBodyFactory;

    public void HandleOneWayMessage(IServiceRemotingRequestMessage requestMessage) => this.LastOneWayMessage = requestMessage;

    public Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(IServiceRemotingRequestContext requestContext, IServiceRemotingRequestMessage requestMessage) => this.requestResponseHandler(requestMessage);
}
