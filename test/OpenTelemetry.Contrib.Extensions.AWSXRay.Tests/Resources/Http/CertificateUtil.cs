// <copyright file="CertificateUtil.cs" company="OpenTelemetry Authors">
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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace OpenTelemetry.Contrib.Extensions.AWSXRay.Tests.Resources.Http
{
    internal class CertificateUtil
    {
        private const string CRTHEADER = "-----BEGIN CERTIFICATE-----\n";
        private const string CRTFOOTER = "\n-----END CERTIFICATE-----";

        public static void CreateCertificate(string certificateName)
        {
            using var rsa = RSA.Create();
            var certRequest = new CertificateRequest("cn=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
            subjectAlternativeNames.AddDnsName("test");
            certRequest.CertificateExtensions.Add(subjectAlternativeNames.Build());

            // Create a temporary certificate and add validity for 1 day
            var certificate = certRequest.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

            var exportData = certificate.Export(X509ContentType.Cert);
            var crt = Convert.ToBase64String(exportData, Base64FormattingOptions.InsertLineBreaks);
            File.WriteAllText(certificateName, CRTHEADER + crt + CRTFOOTER);
        }

        public static void DeleteCertificate(string certificateName)
        {
            if (File.Exists(certificateName))
            {
                File.Delete(certificateName);
            }
        }
    }
}
