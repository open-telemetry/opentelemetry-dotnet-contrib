// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Geneva.Transports;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MetricUnixDataTransport : IMetricDataTransport
{
    private readonly int fixedPayloadLength;
    private readonly UnixDomainSocketDataTransport udsDataTransport;
    private bool isDisposed;

    public MetricUnixDataTransport(
        string unixDomainSocketPath,
        int timeoutMilliseconds = UnixDomainSocketDataTransport.DefaultTimeoutMilliseconds)
    {
        unsafe
        {
            this.fixedPayloadLength = sizeof(BinaryHeader);
        }

        this.udsDataTransport = new UnixDomainSocketDataTransport(unixDomainSocketPath, timeoutMilliseconds);
    }

    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        this.udsDataTransport.Send(body, size + this.fixedPayloadLength);
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

    public void SendOtlpProtobufEvent(byte[] body, int size)
    {
        throw new NotImplementedException();
    }
}
