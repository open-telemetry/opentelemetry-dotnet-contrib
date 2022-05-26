// <copyright file="InstanaSpanTest.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenTelemetry.Exporter.Instana.Tests
{
    internal enum SpanKind
    {
#pragma warning disable SA1602 // Enumeration items should be documented
        ENTRY,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
        EXIT,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
        INTERMEDIATE,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning disable SA1602 // Enumeration items should be documented
        NOT_SET,
#pragma warning restore SA1602 // Enumeration items should be documented
    }

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
    internal class InstanaSpanTransformInfo
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string StatusCode { get; internal set; }

        public string StatusDesc { get; internal set; }

        public bool HasExceptionEvent { get; internal set; }

        public bool IsEntrySpan { get; internal set; }
    }

    internal class InstanaSpanTest
    {
        public InstanaSpanTransformInfo TransformInfo { get; set; }

        [JsonProperty]
        public string N { get; internal set; }

        [JsonProperty]
        public string T { get; internal set; }

        [JsonProperty]
        public string Lt { get; internal set; }

        [JsonProperty]
        public From F { get; internal set; }

        [JsonProperty]
        public string P { get; internal set; }

        [JsonProperty]
        public string S { get; internal set; }

        [JsonProperty]
        public SpanKind K { get; internal set; }

        [JsonProperty]
        public Data Data { get; internal set; }

        [JsonProperty]
        public long Ts { get; internal set; }

        [JsonProperty]
        public long D { get; internal set; }

        [JsonProperty]
        public bool Tp { get; internal set; }

        [JsonProperty]
        public int Ec { get; internal set; }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class From
#pragma warning restore SA1402 // File may only contain a single type
    {
        [JsonProperty]
        public string E { get; internal set; }

        [JsonProperty]
        public string H { get; internal set; }
    }

    [JsonConverter(typeof(DataConverter))]
#pragma warning disable SA1402 // File may only contain a single type
    internal class Data
#pragma warning restore SA1402 // File may only contain a single type
    {
        [JsonProperty]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        public Dictionary<string, string> data { get; internal set; }
#pragma warning restore SA1300 // Element should begin with upper-case letter

        [JsonProperty]
        public Dictionary<string, string> Tags { get; internal set; }

        [JsonProperty]
        public List<SpanEvent> Events { get; internal set; }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class SpanEvent
#pragma warning restore SA1402 // File may only contain a single type
    {
        [JsonProperty]
        public string Name { get; internal set; }

        [JsonProperty]
        public long Ts { get; internal set; }

        [JsonProperty]
        public Dictionary<string, string> Tags { get; internal set; }
    }
}

#pragma warning disable SA1402 // File may only contain a single type
public class DataConverter : JsonConverter
#pragma warning restore SA1402 // File may only contain a single type
{
    public override bool CanWrite => false;

    public override bool CanRead => true;

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(OpenTelemetry.Exporter.Instana.Tests.Data);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return string.Empty;
        }
        else if (reader.TokenType == JsonToken.String)
        {
            return serializer.Deserialize(reader, objectType);
        }
        else
        {
            JObject obj = JObject.Load(reader);
            var data = obj.Root;
            if (data != null)
            {
                var newData = new OpenTelemetry.Exporter.Instana.Tests.Data();
                foreach (var field in data)
                {
                    if (((Newtonsoft.Json.Linq.JProperty)field).Name == "tags" ||
                        ((Newtonsoft.Json.Linq.JProperty)field).Name == "events")
                    {
                        continue;
                    }

                    if (newData.data == null)
                    {
                        newData.data = new Dictionary<string, string>();
                    }

                    newData.data[((Newtonsoft.Json.Linq.JProperty)field).Name] = ((Newtonsoft.Json.Linq.JProperty)field).Value.ToString();
                }

                var existingData = existingValue as OpenTelemetry.Exporter.Instana.Tests.Data ?? new OpenTelemetry.Exporter.Instana.Tests.Data();

                // Populate the remaining standard properties
                using (var subReader = data.CreateReader())
                {
                    serializer.Populate(subReader, existingData);
                }

                newData.Events = existingData.Events;
                newData.Tags = existingData.Tags;

                return newData;
            }

            return null;
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
