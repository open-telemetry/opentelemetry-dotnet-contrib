// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Gcp;

[EventSource(Name = "OpenTelemetry-Resources-Gcp")]
internal sealed class GcpResourcesEventSource : EventSource
{
    public static GcpResourcesEventSource Log = new();

    private const int EventIdFailedToExtractAttributes = 1;

    [NonEvent]
    public void ResourceAttributesExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(EventIdFailedToExtractAttributes, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(EventIdFailedToExtractAttributes, format, exception);
    }
}
