// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.InfluxDB;

/// <summary>
/// EventSource event emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Exporter-InfluxDB")]
internal sealed class InfluxDBEventSource : EventSource
{
    public static InfluxDBEventSource Log = new();

    private InfluxDBEventSource()
    {
    }

    [Event(1, Message = "Failed to export metrics: '{0}'", Level = EventLevel.Error)]
    public void FailedToExport(string exception)
    {
        this.WriteEvent(1, exception);
    }

    [Event(2, Message = "Dropped '{0}' metric writes due to backpressure using mode '{1}'", Level = EventLevel.Warning)]
    public void MetricWritesDropped(int count, string mode)
    {
        this.WriteEvent(2, count, mode);
    }
}
