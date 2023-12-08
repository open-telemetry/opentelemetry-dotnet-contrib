// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
