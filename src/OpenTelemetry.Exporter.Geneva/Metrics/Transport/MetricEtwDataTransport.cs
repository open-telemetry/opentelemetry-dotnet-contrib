// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.Geneva;

[EventSource(Name = "OpenTelemetryGenevaMetricExporter", Guid = "{edc24920-e004-40f6-a8e1-0e6e48f39d84}")]
internal sealed class MetricEtwDataTransport : EventSource, IMetricDataTransport
{
    private const int OtlpProtobufMetricEventId = 81;
    private readonly int fixedPayloadEndIndex;
    private bool isDisposed;

    public static MetricEtwDataTransport Instance { get; private set; } = new();

    private MetricEtwDataTransport()
    {
        unsafe
        {
            this.fixedPayloadEndIndex = sizeof(BinaryHeader);
        }
    }

    [NonEvent]
#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "WriteEventCore is safe when eventData object is a primitive type, which it is in this case.")]
#endif
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

    [NonEvent]
#if NET
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode", Justification = "WriteEventCore is safe when eventData object is a primitive type, which it is in this case.")]
#endif
    public unsafe void SendOtlpProtobufEvent(byte[] data, int size)
    {
        if (this.IsEnabled())
        {
            EventData* descr = stackalloc EventData[1];
            if (data != null && data.Length != 0)
            {
                fixed (byte* blob = data)
                {
                    descr[0].DataPointer = (IntPtr)blob;
                    descr[0].Size = size;
                    this.WriteEventCore(OtlpProtobufMetricEventId, 1, descr);
                }
            }
        }
    }

    [Event(OtlpProtobufMetricEventId)]
    private void OtlpProtobufEvent()
    {
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
            Instance = null;
        }

        this.isDisposed = true;
        base.Dispose(disposing);
    }
}
