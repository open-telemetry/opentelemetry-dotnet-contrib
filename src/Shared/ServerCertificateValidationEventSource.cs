// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK

using System.Diagnostics.Tracing;

namespace OpenTelemetry.ResourceDetectors;

[EventSource(Name = "OpenTelemetry-ResourceDetectors-Http")]
internal class ServerCertificateValidationEventSource : EventSource
{
    public static ServerCertificateValidationEventSource Log = new();

    [Event(1, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(3, format, exception);
    }

    [Event(2, Message = "Failed to validate certificate in format: '{0}', error: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToValidateCertificate(string format, string error)
    {
        this.WriteEvent(4, format, error);
    }
}

#endif
