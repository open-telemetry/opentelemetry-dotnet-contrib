// <copyright file="Handler.cs" company="OpenTelemetry Authors">
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

#if !NETFRAMEWORK

using System;
using System.Net.Http;

namespace OpenTelemetry.ResourceDetectors.AWS.Http;

internal class Handler
{
    public static HttpClientHandler? Create(string certificateFile)
    {
        try
        {
            ServerCertificateValidationProvider? serverCertificateValidationProvider =
                ServerCertificateValidationProvider.FromCertificateFile(certificateFile);

            if (serverCertificateValidationProvider == null)
            {
                AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(Handler), "Failed to Load the certificate file into trusted collection");
                return null;
            }

            var clientHandler = new HttpClientHandler();
            clientHandler.ServerCertificateCustomValidationCallback =
                (sender, x509Certificate2, x509Chain, sslPolicyErrors) =>
                    serverCertificateValidationProvider.ValidationCallback(sender, x509Certificate2, x509Chain, sslPolicyErrors);
            return clientHandler;
        }
        catch (Exception ex)
        {
            AWSResourcesEventSource.Log.ResourceAttributesExtractException($"{nameof(Handler)} : Failed to create HttpClientHandler", ex);
        }

        return null;
    }
}
#endif
