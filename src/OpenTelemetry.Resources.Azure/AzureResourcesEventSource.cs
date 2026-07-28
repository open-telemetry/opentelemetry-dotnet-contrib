// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Azure;

[EventSource(Name = "OpenTelemetry-Resources-Azure")]
internal sealed class AzureResourcesEventSource : EventSource
{
    public static AzureResourcesEventSource Log = new();

    private const int EventIdFailedToDetectAppServiceResources = 1;
    private const int EventIdFailedToDetectAzureContainerAppResources = 2;
    private const int EventIdFailedToDetectAzureVMResources = 3;

    [NonEvent]
    public void FailedToDetectAppServiceResources(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToDetectAppServiceResources(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void FailedToDetectAzureContainerAppResources(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToDetectAzureContainerAppResources(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void FailedToDetectAzureVMResources(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToDetectAzureVMResources(ex.ToInvariantString());
        }
    }

    [Event(EventIdFailedToDetectAppServiceResources, Message = "Failed to detect Azure App Service resources. Exception: {0}", Level = EventLevel.Warning)]
    public void FailedToDetectAppServiceResources(string exception)
        => this.WriteEvent(EventIdFailedToDetectAppServiceResources, exception);

    [Event(EventIdFailedToDetectAzureContainerAppResources, Message = "Failed to detect Azure Container App resources. Exception: {0}", Level = EventLevel.Warning)]
    public void FailedToDetectAzureContainerAppResources(string exception)
        => this.WriteEvent(EventIdFailedToDetectAzureContainerAppResources, exception);

    [Event(EventIdFailedToDetectAzureVMResources, Message = "Failed to detect Azure VM resources. Exception: {0}", Level = EventLevel.Warning)]
    public void FailedToDetectAzureVMResources(string exception)
        => this.WriteEvent(EventIdFailedToDetectAzureVMResources, exception);
}
