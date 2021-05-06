// <copyright file="Span.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Text.Json;

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation.V2
{
    internal readonly struct Span : IJsonSerializable
    {
        public Span(
            string name,
            string traceId,
            string id,
            string parentId,
            long duration,
            long timestamp,
            string type)
        {
            this.Name = name;
            this.TraceId = traceId;
            this.Id = id;
            this.ParentId = parentId;
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

            writer.WritePropertyName(JsonHelper.SpanPropertyName);
            writer.WriteStartObject();

            writer.WriteString(JsonHelper.NamePropertyName, this.Name);
            writer.WriteString(JsonHelper.TraceIdPropertyName, this.TraceId);
            writer.WriteString(JsonHelper.IdPropertyName, this.Id);
            writer.WriteString(JsonHelper.ParentIdPropertyName, this.ParentId);
            writer.WriteNumber(JsonHelper.DurationPropertyName, this.Duration);
            writer.WriteNumber(JsonHelper.TimestampPropertyName, this.Timestamp);
            writer.WriteString(JsonHelper.TypePropertyName, this.Type);

            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
