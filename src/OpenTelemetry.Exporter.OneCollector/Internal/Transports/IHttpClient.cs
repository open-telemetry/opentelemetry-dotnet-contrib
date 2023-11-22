// <copyright file="IHttpClient.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
