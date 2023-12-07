// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK

using OpenTelemetry.ResourceDetectors.AWS.Http;
using Xunit;

namespace OpenTelemetry.ResourceDetectors.AWS.Tests.Http;

public class HandlerTests
{
    private const string INVALIDCRTNAME = "invalidcert";

    [Fact]
    public void TestValidHandler()
    {
        using (CertificateUploader certificateUploader = new CertificateUploader())
        {
            certificateUploader.Create();

            // Validates if the handler created.
            Assert.NotNull(Handler.Create(certificateUploader.FilePath));
        }
    }

    [Fact]
    public void TestInValidHandler()
    {
        // Validates if the handler created if no certificate is loaded into the trusted collection
        Assert.Null(Handler.Create(INVALIDCRTNAME));
    }
}

#endif
