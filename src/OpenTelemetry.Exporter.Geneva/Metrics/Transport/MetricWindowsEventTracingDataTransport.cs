// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Exporter.Geneva.Transports;

namespace OpenTelemetry.Exporter.Geneva;

[EventSource(Name = "OpenTelemetryGenevaMetricExporter", Guid = "{edc24920-e004-40f6-a8e1-0e6e48f39d84}")]
internal sealed class MetricWindowsEventTracingDataTransport : RawEventSource, IMetricDataTransport
{
    private const int OtlpProtobufMetricEventId = 81;
    private readonly int fixedPayloadEndIndex;
    private bool isDisposed;

    private MetricWindowsEventTracingDataTransport()
    {
        this.fixedPayloadEndIndex = BinaryHeader.Size;
    }

    public static MetricWindowsEventTracingDataTransport Instance { get; private set; } = new();

    [NonEvent]
    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        this.WriteRawEvent((int)eventType, body, this.fixedPayloadEndIndex, size);
    }

    [NonEvent]
    public void SendOtlpProtobufEvent(byte[] body, int size)
    {
        if (this.IsEnabled())
        {
            if (body != null && body.Length != 0)
            {
                this.WriteRawEvent(OtlpProtobufMetricEventId, body, 0, size);
            }
        }
    }

#pragma warning disable CA1822 // Mark members as static

    [Event(OtlpProtobufMetricEventId)]
    public void OtlpProtobufEvent()
    {
    }

    [Event((int)MetricEventType.ULongMetric)]
    public void ULongMetricEvent()
    {
    }

    [Event((int)MetricEventType.DoubleMetric)]
    public void DoubleMetricEvent()
    {
    }

    [Event((int)MetricEventType.ExternallyAggregatedULongDistributionMetric)]
    public void ExternallyAggregatedDoubleDistributionMetric()
    {
    }

    [Event((int)MetricEventType.TLV)]
    public void TLVMetricEvent()
    {
    }

#pragma warning restore CA1822 // Mark members as static

    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            // No managed resources to release.
            // The singleton instance is kept alive for the lifetime of the application.
            // Set the instance to null so that future calls to the singleton property can fail explicitly.
            Instance = null!;
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }
}
