// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.ResourceDetectors.AWS.Tests.Http;

internal sealed class NoopServerCertificateValidationEventSource : IServerCertificateValidationEventSource
{
    public static NoopServerCertificateValidationEventSource Instance { get; } = new NoopServerCertificateValidationEventSource();

    public void FailedToExtractResourceAttributes(string format, string exception)
    {
    }

    public void FailedToValidateCertificate(string format, string error)
    {
    }
}
