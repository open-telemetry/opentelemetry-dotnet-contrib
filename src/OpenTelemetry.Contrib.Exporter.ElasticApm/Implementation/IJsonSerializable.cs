using System.Text.Json;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation
{
    internal interface IJsonSerializable
    {
        void Write(Utf8JsonWriter writer);
    }
}
