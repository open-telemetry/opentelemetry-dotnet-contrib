// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.ResourceDetectors;

internal interface IServerCertificateValidationEventSource
{
    public void FailedToExtractResourceAttributes(string format, string exception);

    public void FailedToValidateCertificate(string format, string error);
}
