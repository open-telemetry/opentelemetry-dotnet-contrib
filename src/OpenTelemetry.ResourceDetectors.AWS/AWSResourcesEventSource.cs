// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.ResourceDetectors.AWS;

[EventSource(Name = "OpenTelemetry-ResourceDetectors-AWS")]
internal sealed class AWSResourcesEventSource : EventSource, IServerCertificateValidationEventSource
{
    private const int EVENT_ID_FAILED_TO_EXTRACT_ATTRIBUTES = 1;
    private const int EVENT_ID_FAILED_TO_VALIDATE_CERTIFICATE = 2;
    private const int EVENT_ID_FAILED_TO_CREATE_HTTP_HANDLER = 3;
    private const int EVENT_ID_FAILED_CERTIFICATE_FILE_NOT_EXISTS = 4;
    private const int EVENT_ID_FAILED_TO_LOAD_CERTIFICATE_IN_STORAGE = 5;

    public static AWSResourcesEventSource Log = new();

    [NonEvent]
    public void ResourceAttributesExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(EVENT_ID_FAILED_TO_EXTRACT_ATTRIBUTES, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(EVENT_ID_FAILED_TO_EXTRACT_ATTRIBUTES, format, exception);
    }

    [Event(EVENT_ID_FAILED_TO_VALIDATE_CERTIFICATE, Message = "Failed to validate certificate. Details: '{0}'", Level = EventLevel.Warning)]
    public void FailedToValidateCertificate(string error)
    {
        this.WriteEvent(EVENT_ID_FAILED_TO_VALIDATE_CERTIFICATE, error);
    }

    [Event(EVENT_ID_FAILED_TO_CREATE_HTTP_HANDLER, Message = "Failed to create HTTP handler. Exception: '{0}'", Level = EventLevel.Warning)]
    public void FailedToCreateHttpHandler(Exception exception)
    {
        this.WriteEvent(EVENT_ID_FAILED_TO_CREATE_HTTP_HANDLER, exception.ToInvariantString());
    }

    [Event(EVENT_ID_FAILED_CERTIFICATE_FILE_NOT_EXISTS, Message = "Certificate file does not exist. File: '{0}'", Level = EventLevel.Warning)]
    public void CertificateFileDoesNotExist(string filename)
    {
        this.WriteEvent(EVENT_ID_FAILED_CERTIFICATE_FILE_NOT_EXISTS, filename);
    }

    [Event(EVENT_ID_FAILED_TO_LOAD_CERTIFICATE_IN_STORAGE, Message = "Failed to load certificate in trusted storage. File: '{0}'", Level = EventLevel.Warning)]
    public void FailedToLoadCertificateInTrustedStorage(string filename)
    {
        this.WriteEvent(EVENT_ID_FAILED_TO_LOAD_CERTIFICATE_IN_STORAGE, filename);
    }
}
