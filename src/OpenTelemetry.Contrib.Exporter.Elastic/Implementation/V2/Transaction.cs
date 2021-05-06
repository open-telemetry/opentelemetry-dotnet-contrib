// <copyright file="Transaction.cs" company="OpenTelemetry Authors">
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
    internal readonly struct Transaction : IJsonSerializable
    {
        public Transaction(
            string name,
            string traceId,
            string id,
            string parentId,
            long duration,
            long timestamp,
            string type,
            string result,
            Outcome outcome)
        {
            this.Id = id;
            this.ParentId = parentId;
            this.TraceId = traceId;
            this.Name = name;
            this.Duration = duration;
            this.Timestamp = timestamp;
            this.Type = type;
            this.Result = result;
            this.Outcome = outcome;
        }

        public string Id { get; }

        public string TraceId { get; }

        public string ParentId { get; }

        public string Name { get; }

        public long Duration { get; }

        public long Timestamp { get; }

        public string Type { get; }

        public string Result { get; }

        public Outcome Outcome { get; }

        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(JsonHelper.TransactionPropertyName);
            writer.WriteStartObject();

            writer.WriteString(JsonHelper.NamePropertyName, this.Name);
            writer.WriteString(JsonHelper.TraceIdPropertyName, this.TraceId);
            writer.WriteString(JsonHelper.IdPropertyName, this.Id);

            if (this.ParentId != null)
            {
                writer.WriteString(JsonHelper.ParentIdPropertyName, this.ParentId);
            }

            writer.WriteNumber(JsonHelper.DurationPropertyName, this.Duration);
            writer.WriteNumber(JsonHelper.TimestampPropertyName, this.Timestamp);
            writer.WriteString(JsonHelper.TypePropertyName, this.Type);
            writer.WriteString(JsonHelper.ResultPropertyName, this.Result);
            writer.WriteString(JsonHelper.OutcomePropertyName, this.Outcome.AsString());

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
