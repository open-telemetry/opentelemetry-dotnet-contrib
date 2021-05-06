using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2
{
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
