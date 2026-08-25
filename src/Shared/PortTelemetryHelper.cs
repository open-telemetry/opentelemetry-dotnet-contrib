// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation;

/// <summary>
/// Helper class for caching boxed port numbers to avoid boxing allocations when setting
/// port-valued tags (e.g. <c>server.port</c>, <c>network.peer.port</c>) on a hot request path.
/// </summary>
internal static class PortTelemetryHelper
{
    private static readonly object Port80 = 80;
    private static readonly object Port443 = 443;
    private static readonly object Port8080 = 8080;
    private static readonly object Port5000 = 5000;
    private static readonly object Port5001 = 5001;

    // Single-value cache for any other (e.g. dynamically-assigned) port. A server/client's port is
    // effectively constant for the lifetime of the process, so this avoids boxing on the common path.
    // Reads/writes of a reference are atomic and a race only causes an occasional extra allocation,
    // so no locking is required.
    private static object? lastBoxedPort;

    public static object GetBoxedPort(int port)
    {
        // Reuse pre-boxed instances for the most common ports to avoid allocating.
        var common = port switch
        {
            80 => Port80,
            443 => Port443,
            8080 => Port8080,
            5000 => Port5000,
            5001 => Port5001,
            _ => null,
        };

        if (common is not null)
        {
            return common;
        }

        var last = lastBoxedPort;
        if (last is not null && (int)last == port)
        {
            return last;
        }

        object boxed = port;
        lastBoxedPort = boxed;

        return boxed;
    }
}
