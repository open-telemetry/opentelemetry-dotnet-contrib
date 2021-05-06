using System.Text.Json;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2
{
    internal readonly struct Transaction : IJsonSerializable
    {
        public Transaction(
            string name,
            string traceId,
            string id,
            string parentId,
            long duration,
            long timestamp,
            string type)
        {
            this.Id = id;
            this.ParentId = parentId;
            this.TraceId = traceId;
            this.Name = name;
            this.Duration = duration;
            this.Timestamp = timestamp;
            this.Type = type;
        }

        public string Id { get; }

        public string TraceId { get; }

        public string ParentId { get; }

        public string Name { get; }

        public long Duration { get; }

        public long Timestamp { get; }

        public string Type { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(JsonHelper.TransactionPropertyName);
            writer.WriteStartObject();

            writer.WriteString(JsonHelper.NamePropertyName, this.Name);
            writer.WriteString(JsonHelper.TraceIdPropertyName, this.TraceId);
            writer.WriteString(JsonHelper.IdPropertyName, this.Id);
            writer.WriteString(JsonHelper.ParentIdPropertyName, this.ParentId);
            writer.WriteNumber(JsonHelper.DurationPropertyName, this.Duration);
            writer.WriteNumber(JsonHelper.TimestampPropertyName, this.Timestamp);
            writer.WriteString(JsonHelper.TypePropertyName, this.Type);

            // TODO: Not sure if this can be somehow in OT implemented and how this will influence the APM view.
            writer.WriteStartObject(JsonHelper.SpanCountPropertyName);
            writer.WriteNumber(JsonHelper.DroppedPropertyName, 0);
            writer.WriteNumber(JsonHelper.StartedPropertyName, 0);
            writer.WriteEndObject();

            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
