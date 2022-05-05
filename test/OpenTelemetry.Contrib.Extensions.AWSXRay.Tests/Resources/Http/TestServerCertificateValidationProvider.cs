// <copyright file="TestServerCertificateValidationProvider.cs" company="OpenTelemetry Authors">
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

using System.Security.Cryptography.X509Certificates;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Resources.Http;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests.Resources.Http
{
    [Trait("Platform", "Any")]
    public class TestServerCertificateValidationProvider
    {
        private const string CRTNAME = "cert";
        private const string INVALIDCRTNAME = "invalidcert";

        [Fact]
        public void TestValidCertificate()
        {
            // Creates a self-signed certificate
            CertificateUtil.CreateCertificate(CRTNAME);

            // Loads the certificate to the trusted collection from the file
            ServerCertificateValidationProvider serverCertificateValidationProvider =
                    ServerCertificateValidationProvider.FromCertificateFile(CRTNAME);

            // Validates if the certificate loaded into the trusted collection.
            Assert.True(serverCertificateValidationProvider.IsCertificateLoaded);

            var certificate = new X509Certificate2(CRTNAME);
            X509Chain chain = new X509Chain();
            chain.Build(certificate);

            // validates if certificate is valid
            Assert.True(serverCertificateValidationProvider.ValidationCallback(null, certificate, chain, System.Net.Security.SslPolicyErrors.None));

            // Deletes the certificate
            CertificateUtil.DeleteCertificate(CRTNAME);
        }

        [Fact]
        public void TestInValidCertificate()
        {
            // Loads the certificate to the trusted collection from the file
            ServerCertificateValidationProvider serverCertificateValidationProvider =
                    ServerCertificateValidationProvider.FromCertificateFile(INVALIDCRTNAME);

            // Validates if the certificate file loaded.
            Assert.False(serverCertificateValidationProvider.IsCertificateLoaded);
        }
    }
}
