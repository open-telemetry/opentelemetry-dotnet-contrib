// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.ResourceDetectors.AWS;

[EventSource(Name = "OpenTelemetry-ResourceDetectors-AWS")]
internal sealed class AWSResourcesEventSource : EventSource, IServerCertificateValidationEventSource
{
    public static AWSResourcesEventSource Log = new();

    private const int EVENTIDFAILEDTOEXTRACTATTRIBUTES = 1;
    private const int EVENTIDFAILEDTOVALIDATECERTIFICATE = 2;
    private const int EVENTIDFAILEDTOCREATEHTTPHANDLER = 3;
    private const int EVENTIDFAILEDCERTIFICATEFILENOTEXISTS = 4;
    private const int EVENTIDFAILEDTOLOADCERTIFICATEINSTORAGE = 5;

    [NonEvent]
    public void ResourceAttributesExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(EVENTIDFAILEDTOEXTRACTATTRIBUTES, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(EVENTIDFAILEDTOEXTRACTATTRIBUTES, format, exception);
    }

    [Event(EVENTIDFAILEDTOVALIDATECERTIFICATE, Message = "Failed to validate certificate. Details: '{0}'", Level = EventLevel.Warning)]
    public void FailedToValidateCertificate(string error)
    {
        this.WriteEvent(EVENTIDFAILEDTOVALIDATECERTIFICATE, error);
    }

    [Event(EVENTIDFAILEDTOCREATEHTTPHANDLER, Message = "Failed to create HTTP handler. Exception: '{0}'", Level = EventLevel.Warning)]
    public void FailedToCreateHttpHandler(Exception exception)
    {
        this.WriteEvent(EVENTIDFAILEDTOCREATEHTTPHANDLER, exception.ToInvariantString());
    }

    [Event(EVENTIDFAILEDCERTIFICATEFILENOTEXISTS, Message = "Certificate file does not exist. File: '{0}'", Level = EventLevel.Warning)]
    public void CertificateFileDoesNotExist(string filename)
    {
        this.WriteEvent(EVENTIDFAILEDCERTIFICATEFILENOTEXISTS, filename);
    }

    [Event(EVENTIDFAILEDTOLOADCERTIFICATEINSTORAGE, Message = "Failed to load certificate in trusted storage. File: '{0}'", Level = EventLevel.Warning)]
    public void FailedToLoadCertificateInTrustedStorage(string filename)
    {
        this.WriteEvent(EVENTIDFAILEDTOLOADCERTIFICATEINSTORAGE, filename);
    }
}
