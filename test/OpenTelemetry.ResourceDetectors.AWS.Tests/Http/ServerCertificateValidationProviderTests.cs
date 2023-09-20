// <copyright file="ServerCertificateValidationProviderTests.cs" company="OpenTelemetry Authors">
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

using System.Security.Cryptography.X509Certificates;
using Moq;
using OpenTelemetry.ResourceDetectors.AWS.Http;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.AWS.Tests.Http;

public class ServerCertificateValidationProviderTests
{
    private const string InvalidCertificateName = "invalidcert";

    [Fact]
    public void TestValidCertificate()
    {
        using CertificateUploader certificateUploader = new CertificateUploader();
        certificateUploader.Create();

        var serverCertificateValidationProvider =
            ServerCertificateValidationProvider.FromCertificateFile(certificateUploader.FilePath);

        Assert.NotNull(serverCertificateValidationProvider);

        var certificate = new X509Certificate2(certificateUploader.FilePath);
        X509Chain chain = new X509Chain();
        chain.Build(certificate);

        // validates if certificate is valid
        Assert.NotNull(serverCertificateValidationProvider);
        Assert.NotNull(serverCertificateValidationProvider.ValidationCallback);
        Assert.True(serverCertificateValidationProvider.ValidationCallback(this, certificate, chain, System.Net.Security.SslPolicyErrors.None));
    }

    [Fact]
    public void TestInValidCertificate()
    {
        var serverCertificateValidationProvider =
            ServerCertificateValidationProvider.FromCertificateFile(InvalidCertificateName);

        Assert.Null(serverCertificateValidationProvider);
    }

    [Fact]
    public void TestTestCallbackWithNullCertificate()
    {
        using var certificateUploader = new CertificateUploader();
        certificateUploader.Create();

        var serverCertificateValidationProvider =
            ServerCertificateValidationProvider.FromCertificateFile(certificateUploader.FilePath);

        Assert.NotNull(serverCertificateValidationProvider);
        Assert.False(serverCertificateValidationProvider.ValidationCallback(this, null, Mock.Of<X509Chain>(), default));
    }

    [Fact]
    public void TestCallbackWithNullChain()
    {
        using var certificateUploader = new CertificateUploader();
        certificateUploader.Create();

        var serverCertificateValidationProvider =
            ServerCertificateValidationProvider.FromCertificateFile(certificateUploader.FilePath);

        Assert.NotNull(serverCertificateValidationProvider);
        Assert.False(serverCertificateValidationProvider.ValidationCallback(this, Mock.Of<X509Certificate2>(), null, default));
    }
}

#endif
