// <copyright file="ServerCertificateValidationProvider.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace OpenTelemetry.ResourceDetectors.AWS.Http;

internal class ServerCertificateValidationProvider
{
    private readonly X509Certificate2Collection trustedCertificates;

    private ServerCertificateValidationProvider(X509Certificate2Collection trustedCertificates)
    {
        this.trustedCertificates = trustedCertificates;
        this.ValidationCallback = (_, cert, chain, errors) =>
            this.ValidateCertificate(cert != null ? new X509Certificate2(cert) : null, chain, errors);
    }

    public RemoteCertificateValidationCallback ValidationCallback { get; }

    public static ServerCertificateValidationProvider? FromCertificateFile(string certificateFile)
    {
        if (!File.Exists(certificateFile))
        {
            AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Certificate File does not exist");
            return null;
        }

        var trustedCertificates = new X509Certificate2Collection();
        if (!LoadCertificateToTrustedCollection(trustedCertificates, certificateFile))
        {
            AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to load certificate in trusted collection");
            return null;
        }

        return new ServerCertificateValidationProvider(trustedCertificates);
    }

    private static bool LoadCertificateToTrustedCollection(X509Certificate2Collection collection, string certFileName)
    {
        try
        {
            collection.Import(certFileName);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool HasCommonCertificate(X509Chain chain, X509Certificate2Collection? collection)
    {
        if (collection == null)
        {
            return false;
        }

        foreach (var chainElement in chain.ChainElements)
        {
            foreach (var certificate in collection)
            {
                if (chainElement.Certificate.GetPublicKey().SequenceEqual(certificate.GetPublicKey()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ValidateCertificate(X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors)
    {
        var isSslPolicyPassed = errors == SslPolicyErrors.None ||
                                errors == SslPolicyErrors.RemoteCertificateChainErrors;
        if (!isSslPolicyPassed)
        {
            if ((errors | SslPolicyErrors.RemoteCertificateNotAvailable) == errors)
            {
                AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to validate certificate due to RemoteCertificateNotAvailable");
            }

            if ((errors | SslPolicyErrors.RemoteCertificateNameMismatch) == errors)
            {
                AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to validate certificate due to RemoteCertificateNameMismatch");
            }
        }

        if (chain == null)
        {
            AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to validate certificate. Certificate chain is null.");
            return false;
        }

        if (cert == null)
        {
            AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to validate certificate. Certificate is null.");
            return false;
        }

        chain.ChainPolicy.ExtraStore.AddRange(this.trustedCertificates);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        // building the chain to process basic validations e.g. signature, use, expiration, revocation
        var isValidChain = chain.Build(cert);

        if (!isValidChain)
        {
            var chainErrors = string.Empty;
            foreach (var element in chain.ChainElements)
            {
                foreach (var status in element.ChainElementStatus)
                {
                    chainErrors +=
                        $"\nCertificate [{element.Certificate.Subject}] Status [{status.Status}]: {status.StatusInformation}";
                }
            }

            AWSResourcesEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), $"Failed to validate certificate due to {chainErrors}");
        }

        // check if at least one certificate in the chain is in our trust list
        var isTrusted = HasCommonCertificate(chain, this.trustedCertificates);
        if (!isTrusted)
        {
            var serverCertificates = string.Empty;
            foreach (var element in chain.ChainElements)
            {
                serverCertificates += " " + element.Certificate.Subject;
            }

            var trustCertificates = string.Empty;
            foreach (var trustCertificate in this.trustedCertificates)
            {
                trustCertificates += " " + trustCertificate.Subject;
            }

            AWSResourcesEventSource.Log.FailedToValidateCertificate(
                nameof(ServerCertificateValidationProvider),
                $"Server Certificates Chain cannot be trusted. The chain doesn't match with the Trusted Certificates provided. Server Certificates:{serverCertificates}. Trusted Certificates:{trustCertificates}");
        }

        return isSslPolicyPassed && isValidChain && isTrusted;
    }
}
#endif
