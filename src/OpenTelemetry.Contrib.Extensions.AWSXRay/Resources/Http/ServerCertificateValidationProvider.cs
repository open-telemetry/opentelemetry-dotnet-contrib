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

using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Resources.Http
{
    internal class ServerCertificateValidationProvider
    {
        private static readonly ServerCertificateValidationProvider InvalidProvider =
            new ServerCertificateValidationProvider(null);

        private readonly X509Certificate2Collection trustedCertificates;

        private ServerCertificateValidationProvider(X509Certificate2Collection trustedCertificates)
        {
            if (trustedCertificates == null)
            {
                this.trustedCertificates = null;
                this.ValidationCallback = null;
                this.IsCertificateLoaded = false;
                return;
            }

            this.trustedCertificates = trustedCertificates;
            this.ValidationCallback = (sender, cert, chain, errors) =>
                this.ValidateCertificate(new X509Certificate2(cert), chain, errors);
            this.IsCertificateLoaded = true;
        }

        public bool IsCertificateLoaded { get; }

        public RemoteCertificateValidationCallback ValidationCallback { get; }

        public static ServerCertificateValidationProvider FromCertificateFile(string certificateFile)
        {
            if (!File.Exists(certificateFile))
            {
                AWSXRayEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Certificate File does not exist");
                return InvalidProvider;
            }

            var trustedCertificates = new X509Certificate2Collection();
            if (!LoadCertificateToTrustedCollection(trustedCertificates, certificateFile))
            {
                AWSXRayEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to load certificate in trusted collection");
                return InvalidProvider;
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

        private bool ValidateCertificate(X509Certificate2 cert, X509Chain chain, SslPolicyErrors errors)
        {
            var isSslPolicyPassed = errors == SslPolicyErrors.None ||
                                    errors == SslPolicyErrors.RemoteCertificateChainErrors;
            if (!isSslPolicyPassed)
            {
                if ((errors | SslPolicyErrors.RemoteCertificateNotAvailable) == errors)
                {
                    AWSXRayEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to validate certificate due to RemoteCertificateNotAvailable");
                }

                if ((errors | SslPolicyErrors.RemoteCertificateNameMismatch) == errors)
                {
                    AWSXRayEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), "Failed to validate certificate due to RemoteCertificateNameMismatch");
                }
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

                AWSXRayEventSource.Log.FailedToValidateCertificate(nameof(ServerCertificateValidationProvider), $"Failed to validate certificate due to {chainErrors}");
            }

            // check if at least one certificate in the chain is in our trust list
            var isTrusted = this.HasCommonCertificate(chain, this.trustedCertificates);
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

                AWSXRayEventSource.Log.FailedToValidateCertificate(
                    nameof(ServerCertificateValidationProvider),
                    $"Server Certificates Chain cannot be trusted. The chain doesn't match with the Trusted Certificates provided. Server Certificates:{serverCertificates}. Trusted Certificates:{trustCertificates}");
            }

            return isSslPolicyPassed && isValidChain && isTrusted;
        }

        private bool HasCommonCertificate(X509Chain chain, X509Certificate2Collection collection)
        {
            foreach (var chainElement in chain.ChainElements)
            {
                foreach (var certificate in collection)
                {
                    if (Enumerable.SequenceEqual(chainElement.Certificate.GetPublicKey(), certificate.GetPublicKey()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
