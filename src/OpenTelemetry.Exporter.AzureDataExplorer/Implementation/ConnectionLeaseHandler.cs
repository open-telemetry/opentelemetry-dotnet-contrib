// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Collections.Concurrent;
using System.Net;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// On .NET Framework, <see cref="HttpClientHandler"/> keeps connections open
/// indefinitely so DNS changes are never observed. This handler sets
/// <see cref="ServicePoint.ConnectionLeaseTimeout"/> for each endpoint the
/// exporter talks to, which is the .NET Framework equivalent of
/// <c>SocketsHttpHandler.PooledConnectionLifetime</c>.
/// </summary>
internal sealed class ConnectionLeaseHandler : DelegatingHandler
{
    internal static readonly TimeSpan ConnectionLeaseTimeout = TimeSpan.FromMinutes(5);

    private static readonly ConcurrentDictionary<string, bool> ConfiguredEndpoints = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionLeaseHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    internal static void ConfigureServicePoint(Uri requestUri)
    {
        if (!requestUri.IsAbsoluteUri)
        {
            return;
        }

        var endpoint = requestUri.GetLeftPart(UriPartial.Authority);
        if (ConfiguredEndpoints.ContainsKey(endpoint))
        {
            return;
        }

        try
        {
            var servicePoint = ServicePointManager.FindServicePoint(requestUri);
            servicePoint.ConnectionLeaseTimeout = (int)ConnectionLeaseTimeout.TotalMilliseconds;
        }
        catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException or InvalidOperationException)
        {
            // Best effort: some platforms (for example .NET Core running the netstandard build) do not use service points.
        }

        ConfiguredEndpoints.TryAdd(endpoint, true);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri != null)
        {
            ConfigureServicePoint(request.RequestUri);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
#endif
