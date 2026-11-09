// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources.Container;

[EventSource(Name = "OpenTelemetry-Resources-Container")]
internal sealed class ContainerExtensionsEventSource : EventSource
{
    public static ContainerExtensionsEventSource Log = new();

    [NonEvent]
    public void ExtractResourceAttributesException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
        {
            this.FailedToExtractResourceAttributes(format, ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void MultipleContainerIdsFound(IReadOnlyCollection<string> containerIds)
    {
        if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
        {
            this.MultipleContainerIdsFound(string.Join(", ", containerIds));
        }
    }

    [Event(1, Message = "Failed to extract resource attributes in '{0}'.", Level = EventLevel.Error)]
    public void FailedToExtractResourceAttributes(string format, string exception)
    {
        this.WriteEvent(1, format, exception);
    }

    [Event(2, Message = "Multiple container ids found in the cgroup v2 mount information, using the last one: {0}.", Level = EventLevel.Warning)]
    public void MultipleContainerIdsFound(string containerIds)
    {
        this.WriteEvent(2, containerIds);
    }

    [Event(3, Message = "Ignoring the id of the pod sandbox '{0}' in the cgroup v2 mount information as it is not the id of the container, so the container id could not be determined.", Level = EventLevel.Informational)]
    public void SandboxContainerIdIgnored(string sandboxId)
    {
        this.WriteEvent(3, sandboxId);
    }
}
