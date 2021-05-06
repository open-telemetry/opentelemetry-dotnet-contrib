using System.Text.Json;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2
{
    internal readonly struct Service
    {
        public Service(string name, string environment, Agent agent)
        {
            this.Name = name;
            this.Environment = environment;
            this.Agent = agent;
        }

        internal string Name { get; }

        internal string Environment { get; }

        internal Agent Agent { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WriteString(JsonHelper.NamePropertyName, this.Name);
            writer.WriteString(JsonHelper.EnvironmentPropertyName, this.Environment);
            writer.WritePropertyName(JsonHelper.AgentPropertyName);
            this.Agent.Write(writer);

            writer.WriteEndObject();
        }
    }
}
