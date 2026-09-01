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

    // Single-value cache for any other (e.g. dynamically-assigned) port. Only useful when the caller's
    // port is effectively constant for the lifetime of the process (e.g. a server's own listening
    // port) so that this avoids boxing on the common path. Reads/writes of a reference are atomic and
    // a race only causes an occasional extra allocation, so no locking is required. Callers whose port
    // varies per call (e.g. a client targeting many different remote ports) should not opt in.
    private static object? lastBoxedPort;

    /// <summary>
    /// Gets a boxed instance of <paramref name="port"/>, avoiding a fresh boxing allocation where possible.
    /// </summary>
    /// <param name="port">The port number to box.</param>
    /// <param name="cacheValue">
    /// Whether to fall back to the single-value cache when <paramref name="port"/> is not one of the
    /// common, pre-boxed ports. Only pass <see langword="true"/> when the port is expected to be
    /// effectively constant for the process's lifetime; otherwise the cache will rarely hit.
    /// </param>
    /// <returns>A boxed <see cref="int"/> equal to <paramref name="port"/>.</returns>
    public static object GetBoxedPort(int port, bool cacheValue = false)
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

        if (!cacheValue)
        {
            return port;
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
