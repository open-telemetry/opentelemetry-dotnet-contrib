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
    public KubeApiConnector(string kubeHost, string kubePort, string certFile, string token, string nameSpace, string kubeHostName)
    {
#if !NETFRAMEWORK
        httpClientHandler = Handler.Create(certFile);
#else
        // httpclienthandler does not have a way to apply the certificate in .net framework 4.6.2
        httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Automatic;
#endif
        httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        this.Target = new Uri($"https://{kubeHost}:{kubePort}/api/v1/namespaces/{nameSpace}/pods/{kubeHostName}", UriKind.Absolute);
    }

    public override Uri Target { get; }
}
