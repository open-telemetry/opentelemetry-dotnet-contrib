using System.Text.Json;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2
{
    internal readonly struct Metadata : IJsonSerializable
    {
        public Metadata(Service service)
        {
            this.Service = service;
        }

        internal Service Service { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(JsonHelper.MetadataPropertyName);
            writer.WriteStartObject();

            writer.WritePropertyName(JsonHelper.ServicePropertyName);
            this.Service.Write(writer);

            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
