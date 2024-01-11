// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Net.Http;
using System.Net.Http.Headers;
#if !NETFRAMEWORK
using OpenTelemetry.ResourceDetectors.Container.Http;
#endif

namespace OpenTelemetry.ResourceDetectors.Container;

internal class KubeApiConnector : ApiConnector
{
    // Create custom Apache Http Client. Just like we are doing in MTAgent
    // Simple
    // Wrapper (from controller api) doesn't provide a way to create custom SSLContext.
    public KubeApiConnector(string? kubeHost, string? kubePort, string? certFile, string? token, string? nameSpace, string? kubeHostName)
    {
#if !NETFRAMEWORK
        if (certFile != null)
        {
            this.ClientHandler = Handler.Create(certFile);
        }

        this.ClientHandler ??= new()
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; },
            };
#else
        this.ClientHandler = new HttpClientHandler();
#endif

        httpClient = new HttpClient(this.ClientHandler);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        this.Target = new Uri($"https://{kubeHost}:{kubePort}/api/v1/namespaces/{nameSpace}/pods/{kubeHostName}");
    }

    public override HttpClientHandler? ClientHandler { get; }

    public override Uri Target { get; }
}
