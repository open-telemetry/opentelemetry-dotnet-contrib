// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.ResourceDetectors.Container.Http;

namespace OpenTelemetry.ResourceDetectors.Container;

internal class KubeApiConnector
{
    protected const int MaxAttempts = 3;

    protected static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    protected static readonly HashSet<HttpStatusCode> AcceptableResponseCodes =
        [HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden];

    protected static HttpClient httpClient = new();

    protected int connectionTimeout = 5;

    public KubeApiConnector(string? kubeHost, string? kubePort, string? certFile, string? token, string? nameSpace, string? kubeHostName)
    {
        this.Target = new Uri($"https://{kubeHost}:{kubePort}/api/v1/namespaces/{nameSpace}/pods/{kubeHostName}");

        if (certFile == null)
        {
            return;
        }

        using HttpClientHandler? clientHandler = Handler.Create(certFile);
        if (clientHandler == null)
        {
            return;
        }

        clientHandler.CheckCertificateRevocationList = true;

        httpClient = new HttpClient(clientHandler);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Uri Target { get; }

    public string ExecuteRequest()
    {
        // Give arbitrary amount of time for the kube api to update container status
        Thread.Sleep(TimeSpan.FromSeconds(FiveSeconds.TotalSeconds));
        return this.ExecuteHttpRequest().Result;
    }

    public async Task<string> ExecuteHttpRequest()
    {
        Uri uri = this.Target;

        try
        {
            httpClient.Timeout = TimeSpan.FromSeconds(this.connectionTimeout);
            using HttpResponseMessage response = await httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return responseBody;
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }
}
#endif
