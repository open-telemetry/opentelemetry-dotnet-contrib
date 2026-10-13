// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

/// <summary>
/// A small, bounded cache of the <c>server.address</c> and <c>server.port</c> values derived from
/// the <c>Host</c> of incoming requests.
/// </summary>
internal static class ServerHostCache
{
    internal const int Capacity = 4; // Must be a power of two

    private static readonly Entry?[] Entries = new Entry?[Capacity];
    private static int nextIndex;

    /// <summary>
    /// Gets the cached <c>server.address</c> and <c>server.port</c> values for the specified host.
    /// </summary>
    /// <param name="host">The <see cref="HostString"/> of the request.</param>
    /// <returns>
    /// The <see cref="Entry"/> containing the value of <see cref="HostString.Host"/> and the boxed
    /// value of <see cref="HostString.Port"/> for <paramref name="host"/>.
    /// </returns>
    public static Entry Get(HostString host)
    {
        var value = host.Value!;
        var entries = Entries;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = Volatile.Read(ref entries[i]);
            if (entry is not null && string.Equals(entry.Value, value, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        var port = host.Port;
        var created = new Entry(value, host.Host, port is { } p ? PortTelemetryHelper.GetBoxedPort(p, cacheValue: true) : null);

        var index = unchecked(nextIndex++) & (Capacity - 1);
        Volatile.Write(ref entries[index], created);

        return created;
    }

    internal sealed class Entry(string value, string address, object? port)
    {
        /// <summary>
        /// Gets the value of the <see cref="HostString"/> the entry was created from.
        /// </summary>
        public string Value { get; } = value;

        /// <summary>
        /// Gets the value for the <c>server.address</c> attribute.
        /// </summary>
        public string Address { get; } = address;

        /// <summary>
        /// Gets the boxed value for the <c>server.port</c> attribute, if the host specified a port.
        /// </summary>
        public object? Port { get; } = port;
    }
}
