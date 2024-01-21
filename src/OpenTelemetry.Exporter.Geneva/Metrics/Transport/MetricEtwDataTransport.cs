// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.Geneva;

[EventSource(Name = "OpenTelemetryGenevaMetricExporter", Guid = "{edc24920-e004-40f6-a8e1-0e6e48f39d84}")]
internal sealed class MetricEtwDataTransport : EventSource, IMetricDataTransport
{
    private readonly int fixedPayloadEndIndex;

    static MetricEtwDataTransport()
    {
        Shared = new();
    }

    public static readonly MetricEtwDataTransport Shared;

    private MetricEtwDataTransport()
    {
        unsafe
        {
            this.fixedPayloadEndIndex = sizeof(BinaryHeader);
        }
    }

    [NonEvent]
    public unsafe void Send(MetricEventType eventType, byte[] data, int size)
    {
        var eventDataPtr = stackalloc EventData[1];
        fixed (byte* bufferPtr = data)
        {
            eventDataPtr[0].DataPointer = (IntPtr)bufferPtr + this.fixedPayloadEndIndex;
            eventDataPtr[0].Size = size;
            this.WriteEventCore((int)eventType, 1, eventDataPtr);
        }
    }

    [Event((int)MetricEventType.ULongMetric)]
    private void ULongMetricEvent()
    {
    }

    [Event((int)MetricEventType.DoubleMetric)]
    private void DoubleMetricEvent()
    {
    }

    [Event((int)MetricEventType.ExternallyAggregatedULongDistributionMetric)]
    private void ExternallyAggregatedDoubleDistributionMetric()
    {
    }

    [Event((int)MetricEventType.TLV)]
    private void TLVMetricEvent()
    {
    }
}
