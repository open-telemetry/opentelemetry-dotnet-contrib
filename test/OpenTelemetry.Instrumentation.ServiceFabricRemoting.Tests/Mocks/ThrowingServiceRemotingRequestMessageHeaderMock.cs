// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.ServiceFabric.Services.Remoting.V2;

namespace OpenTelemetry.Instrumentation.ServiceFabricRemoting.Tests;

/// <summary>
/// A request message header that rejects every header, to simulate a failure while propagating the trace context.
/// </summary>
internal sealed class ThrowingServiceRemotingRequestMessageHeaderMock : IServiceRemotingRequestMessageHeader
{
    public const string ErrorMessage = "Headers cannot be added";

    public int MethodId { get; set; }

    public int InterfaceId { get; set; }

    public string? InvocationId { get; set; }

    public string? MethodName { get; set; }

    public Guid RequestId { get; set; }

    public void AddHeader(string headerName, byte[] headerValue) => throw new InvalidOperationException(ErrorMessage);

    public bool TryGetHeaderValue(string headerName, out byte[]? headerValue)
    {
        headerValue = null;

        return false;
    }
}
