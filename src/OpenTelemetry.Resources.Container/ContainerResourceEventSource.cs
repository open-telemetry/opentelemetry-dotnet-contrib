// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Container;

[EventSource(Name = "OpenTelemetry-Resources-Container")]
internal class ContainerResourceEventSource : EventSource, IServerCertificateValidationEventSource
{
    public static ContainerResourceEventSource Log = new();

    private const int EventIdFailedToExtractResourceAttributes = 1;
    private const int EventIdFailedToValidateCertificate = 2;
    private const int EventIdFailedToCreateHttpHandler = 3;
    private const int EventIdFailedCertificateFileNotExists = 4;
    private const int EventIdFailedToLoadCertificateInStorage = 5;

    [NonEvent]
    public void ExtractResourceAttributesException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(EventIdFailedToExtractResourceAttributes, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Error)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(EventIdFailedToExtractResourceAttributes, format, exception);
    }

    [Event(EventIdFailedToValidateCertificate, Message = "Failed to validate certificate. Details: '{0}'", Level = EventLevel.Warning)]
    public void FailedToValidateCertificate(string error)
    {
        this.WriteEvent(EventIdFailedToValidateCertificate, error);
    }

    [Event(EventIdFailedToCreateHttpHandler, Message = "Failed to create HTTP handler. Exception: '{0}'", Level = EventLevel.Warning)]
    public void FailedToCreateHttpHandler(Exception exception)
    {
        this.WriteEvent(EventIdFailedToCreateHttpHandler, exception.ToInvariantString());
    }

    [Event(EventIdFailedCertificateFileNotExists, Message = "Certificate file does not exist. File: '{0}'", Level = EventLevel.Warning)]
    public void CertificateFileDoesNotExist(string filename)
    {
        this.WriteEvent(EventIdFailedCertificateFileNotExists, filename);
    }

    [Event(EventIdFailedToLoadCertificateInStorage, Message = "Failed to load certificate in trusted storage. File: '{0}'", Level = EventLevel.Warning)]
    public void FailedToLoadCertificateInTrustedStorage(string filename)
    {
        this.WriteEvent(EventIdFailedToLoadCertificateInStorage, filename);
    }
}
