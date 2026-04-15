// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.OpAmp.Client.Internal.Transport;

internal static class TransportConstants
{
    /// <summary>
    /// Maximum allowed size of a single OpAMP message received from the server (128 KB).
    /// Applies to both HTTP and WebSocket transports. Responses exceeding this limit
    /// are rejected to prevent uncontrolled memory allocation (CWE-789).
    /// </summary>
    public const int MaxMessageSize = 128 * 1024;
}
