using System;

namespace OpenTelemetry.Exporter.Geneva;

internal sealed class MetricTcpDataTransport : IMetricDataTransport
{
    private readonly int fixedPayloadLength;
    private readonly TcpSocketDataTransport udsDataTransport;
    private bool isDisposed;

    public MetricTcpDataTransport(string host, int port, Action onTcpConnectionSuccess, Action<Exception> onTcpConnectionFailure)
    {
        unsafe
        {
            this.fixedPayloadLength = sizeof(IfxBinaryHeader);
        }

        this.udsDataTransport = new TcpSocketDataTransport(host, port, onTcpConnectionSuccess, onTcpConnectionFailure);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.udsDataTransport?.Dispose();
        this.isDisposed = true;
    }

    public void Send(MetricEventType eventType, byte[] body, int size)
    {
        this.udsDataTransport.Send(body, size + this.fixedPayloadLength);
    }
}
