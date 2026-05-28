// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Geneva.Transports;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MetricUnixDomainSocketDataTransport : IMetricDataTransport
{
    private readonly int fixedPayloadLength;
    private readonly UnixDomainSocketDataTransport udsDataTransport;
    private readonly byte[] batchBuffer;
    private int batchBufferOffset;
    private bool isDisposed;

    public MetricUnixDomainSocketDataTransport(
        string unixDomainSocketPath,
        int timeoutMilliseconds = UnixDomainSocketDataTransport.DefaultTimeoutMilliseconds)
    {
        unsafe
        {
            this.fixedPayloadLength = sizeof(BinaryHeader);
        }

        this.udsDataTransport = new UnixDomainSocketDataTransport(unixDomainSocketPath, timeoutMilliseconds);

        // Bound the accumulation buffer to the same size used by the OTLP
        // serializer for a single event. The UDS socket is SOCK_STREAM and
        // the receiver (MDSD) demultiplexes events via the uint32-LE length
        // prefix already written by the serializer, so concatenating events
        // into one Send is wire-compatible with the unbatched format.
        this.batchBuffer = new byte[GenevaMetricExporter.BufferSize];
    }

    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        this.udsDataTransport.Send(body, size + this.fixedPayloadLength);
    }

    public void SendOtlpProtobufEvent(byte[] body, int size)
    {
        this.udsDataTransport.Send(body, size);
    }

    public bool TryAppendOtlpProtobufEvent(byte[] body, int size)
    {
        if (size <= 0)
        {
            return true;
        }

        if (size > this.batchBuffer.Length - this.batchBufferOffset)
        {
            return false;
        }

        Buffer.BlockCopy(body, 0, this.batchBuffer, this.batchBufferOffset, size);
        this.batchBufferOffset += size;
        return true;
    }

    public void FlushOtlpProtobufEvents()
    {
        var bytesToSend = this.batchBufferOffset;
        if (bytesToSend == 0)
        {
            return;
        }

        // Reset accumulation before sending so that a partially-buffered
        // batch cannot leak into the next export if Send throws.
        this.batchBufferOffset = 0;
        this.udsDataTransport.Send(this.batchBuffer, bytesToSend);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.udsDataTransport.Dispose();
        this.isDisposed = true;
    }
}
