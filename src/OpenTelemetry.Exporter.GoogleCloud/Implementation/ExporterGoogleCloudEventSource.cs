// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.GoogleCloud.Implementation;

[EventSource(Name = "OpenTelemetry-Exporter-Stackdriver")]
internal class ExporterGoogleCloudEventSource : EventSource
{
    public static readonly ExporterGoogleCloudEventSource Log = new();

    [NonEvent]
    public void ExportMethodException(Exception ex)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExportMethodException(ex.ToInvariantString());
        }
    }

    [Event(3, Message = "Stackdriver exporter encountered an error while exporting. Exception: {0}", Level = EventLevel.Error)]
    public void ExportMethodException(string ex)
    {
        this.WriteEvent(1, ex);
    }
}
