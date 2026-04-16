// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Transport;

/// <summary>
/// Thrown when an OpAMP server response body exceeds <see cref="TransportConstants.MaxMessageSize"/>.
/// Distinct from a send failure - the HTTP request was accepted by the server, but the client
/// deliberately discarded the oversized response to prevent uncontrolled memory allocation.
/// </summary>
internal sealed class OpAmpOversizedResponseException : InvalidOperationException
{
    public OpAmpOversizedResponseException()
    {
    }

    public OpAmpOversizedResponseException(string message)
        : base(message)
    {
    }

    public OpAmpOversizedResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
