namespace OpenTelemetry.Exporter.Geneva;

internal interface IDataTransport
{
    bool IsEnabled();

    void Send(byte[] data, int size);
}
