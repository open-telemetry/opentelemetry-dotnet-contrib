// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OneCollector;

internal interface IHttpClient
{
    HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken);
}

internal sealed class HttpClientWrapper : IHttpClient
{
    private readonly HttpClient httpClient;

    public HttpClientWrapper(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        return this.httpClient.Send(request, CancellationToken.None);
#else
        return this.httpClient.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
#endif
    }
}
