// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK

using System;
using System.Net.Http;
using OpenTelemetry.Internal;

namespace OpenTelemetry.ResourceDetectors;

internal class ServerCertificateValidationHandler
{
    public static HttpClientHandler? Create(string certificateFile)
    {
        try
        {
            ServerCertificateValidationProvider? serverCertificateValidationProvider = ServerCertificateValidationProvider.FromCertificateFile(certificateFile);

            if (serverCertificateValidationProvider == null)
            {
                ServerCertificateValidationEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationHandler), "Failed to Load the certificate file into trusted collection");
                return null;
            }

            var clientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                (sender, x509Certificate2, x509Chain, sslPolicyErrors) =>
                    serverCertificateValidationProvider.ValidationCallback(sender, x509Certificate2, x509Chain, sslPolicyErrors),
            };
            return clientHandler;
        }
        catch (Exception ex)
        {
            ServerCertificateValidationEventSource.Log.FailedToExtractResourceAttributes($"{nameof(ServerCertificateValidationHandler)} : Failed to create HttpClientHandler", ex.ToInvariantString());
        }

        return null;
    }
}
#endif
