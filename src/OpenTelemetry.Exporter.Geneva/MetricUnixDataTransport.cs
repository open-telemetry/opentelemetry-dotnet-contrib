namespace OpenTelemetry.Exporter.Geneva
{
    internal sealed class MetricUnixDataTransport : IMetricDataTransport
    {
        private bool isDisposed;
        private readonly int fixedPayloadLength;
        private readonly UnixDomainSocketDataTransport udsDataTransport;

        public MetricUnixDataTransport(string unixDomainSocketPath,
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
            if (isDisposed)
            {
                return;
            }

            udsDataTransport?.Dispose();
            isDisposed = true;
        }
    }
}
