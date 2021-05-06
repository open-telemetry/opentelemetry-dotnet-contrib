using System.Diagnostics;
using System.Reflection;
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

    internal readonly struct Agent
    {
        public Agent(Assembly assembly)
        {
            this.Name = "opentelemetry";
            this.Version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        }

        internal string Name { get; }

        internal string Version { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WriteString(JsonHelper.NamePropertyName, this.Name);
            writer.WriteString(JsonHelper.VersionPropertyName, this.Version);

            writer.WriteEndObject();
        }
    }
}
