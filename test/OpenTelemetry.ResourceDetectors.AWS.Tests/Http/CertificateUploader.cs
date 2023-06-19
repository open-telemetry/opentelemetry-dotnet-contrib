// <copyright file="CertificateUploader.cs" company="OpenTelemetry Authors">
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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace OpenTelemetry.ResourceDetectors.AWS.Tests.Http;

internal class CertificateUploader : IDisposable
{
    private const string CRTHEADER = "-----BEGIN CERTIFICATE-----\n";
    private const string CRTFOOTER = "\n-----END CERTIFICATE-----";
    private string filePath;

    public CertificateUploader()
    {
        this.filePath = Path.GetTempFileName();
    }

    public string FilePath
    {
        get { return this.filePath; }
        set { this.filePath = value; }
    }

    public void Create()
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

        using (FileStream stream = new FileStream(this.filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            using (StreamWriter sw = new StreamWriter(stream))
            {
                sw.Write(CRTHEADER + crt + CRTFOOTER);
            }
        }
    }

    public void Dispose()
    {
        for (int tries = 0; ; tries++)
        {
            try
            {
                File.Delete(this.filePath);
                return;
            }
            catch (IOException) when (tries < 3)
            {
                // the file is unavailable because it is: still being written to or being processed by another thread
                // sleep for sometime before deleting
                Thread.Sleep(1000);
            }
        }
    }
}

#endif
