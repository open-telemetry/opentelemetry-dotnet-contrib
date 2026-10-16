// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.ServiceFabric.Services.Remoting.V2;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

internal sealed class DisposableServiceRemotingMessageHandlerMock : ServiceRemotingMessageHandlerMock, IDisposable
{
    public DisposableServiceRemotingMessageHandlerMock(Func<IServiceRemotingRequestMessage, Task<IServiceRemotingResponseMessage>> requestResponseHandler, IServiceRemotingMessageBodyFactory messageBodyFactory)
        : base(requestResponseHandler, messageBodyFactory)
    {
    }

    public bool IsDisposed { get; private set; }

    public void Dispose() => this.IsDisposed = true;
}
