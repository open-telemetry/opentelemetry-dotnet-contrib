// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net.Http;

namespace OpenTelemetry.ResourceDetectors.Container;

internal class KubeApiConnector : ApiConnector
{
    // Create custom Apache Http Client. Just like we are doing in MTAgent
    // Simple
    // Wrapper (from controller api) doesn't provide a way to create custom SSLContext.
    public KubeApiConnector(string kubeHost, string kubePort, string certFile, string token, string nameSpace, string kubeHostName)
    {
        // WebRequest request = WebRequest.Create("www.contoso.com");


        //Communicator = HttpCommunicator.Create(new HttpCommunicatorSettings(
        //    new Uri($"https://{kubeHost}:{kubePort}"),
        //    null,
        //    false,
        //    new CustomSslCertificateCheckSettings(certFile, null),
        //    false,
        //    false,
        //    new AuthenticationSettings(null, token, AuthenticationSettings.AuthenticationType.ForceBearer),
        //    null));

        // TARGET_FORMAT = https://"$KUBERNETES_SERVICE_HOST":"$KUBERNETES_SERVICE_PORT"/api/v1/namespaces/javaspace/pods/app-deployment-547d9ffffc-97xrd
        Target = new Uri($"api/v1/namespaces/{nameSpace}/pods/{kubeHostName}", UriKind.Relative);
    }

    public override Uri Target { get; }
}
