// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.ResourceDetectors.AWS;

[EventSource(Name = "OpenTelemetry-ResourceDetectors-AWS")]
internal sealed class AWSResourcesEventSource : EventSource
{
    public static AWSResourcesEventSource Log = new();

    [NonEvent]
    public void ResourceAttributesExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Warning)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(3, format, exception);
    }
}
