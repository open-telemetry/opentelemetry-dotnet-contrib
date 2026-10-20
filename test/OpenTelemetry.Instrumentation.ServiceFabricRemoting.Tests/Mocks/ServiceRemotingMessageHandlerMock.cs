// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Runtime;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

internal class ServiceRemotingMessageHandlerMock : IServiceRemotingMessageHandler
{
    private readonly IServiceRemotingMessageBodyFactory messageBodyFactory;

    public ServiceRemotingMessageHandlerMock(IServiceRemotingMessageBodyFactory messageBodyFactory)
    {
        this.messageBodyFactory = messageBodyFactory;
    }

    /// <summary>
    /// Gets or sets the response to return from <see cref="HandleRequestResponseAsync"/>.
    /// </summary>
    public IServiceRemotingResponseMessage? ResponseMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception to throw from <see cref="HandleRequestResponseAsync"/> instead of returning <see cref="ResponseMessage"/>.
    /// </summary>
    public Exception? Exception { get; set; }

    public IServiceRemotingRequestMessage? LastOneWayMessage { get; private set; }

    public IServiceRemotingMessageBodyFactory GetRemotingMessageBodyFactory() => this.messageBodyFactory;

    public void HandleOneWayMessage(IServiceRemotingRequestMessage requestMessage) => this.LastOneWayMessage = requestMessage;

    public Task<IServiceRemotingResponseMessage> HandleRequestResponseAsync(IServiceRemotingRequestContext requestContext, IServiceRemotingRequestMessage requestMessage)
        => this.Exception != null
            ? Task.FromException<IServiceRemotingResponseMessage>(this.Exception)
            : Task.FromResult(this.ResponseMessage!);
}
