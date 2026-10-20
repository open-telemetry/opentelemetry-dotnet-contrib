// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Fabric;
using Microsoft.ServiceFabric.Services.Remoting.V2;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

/// <summary>
/// An <see cref="IServiceRemotingClient"/> whose requests fail, which ServiceFabric.Mocks' MockActorServiceRemotingClient cannot simulate.
/// </summary>
internal sealed class ThrowingServiceRemotingClientMock : IServiceRemotingClient
{
    private readonly Exception exception;

    public ThrowingServiceRemotingClientMock(Exception exception)
    {
        this.exception = exception;
    }

    public ResolvedServicePartition? ResolvedServicePartition { get; set; }

    public string? ListenerName { get; set; }

    public ResolvedServiceEndpoint? Endpoint { get; set; }

    public Task<IServiceRemotingResponseMessage> RequestResponseAsync(IServiceRemotingRequestMessage requestMessage) => Task.FromException<IServiceRemotingResponseMessage>(this.exception);

    public void SendOneWay(IServiceRemotingRequestMessage requestMessage) => throw this.exception;
}
