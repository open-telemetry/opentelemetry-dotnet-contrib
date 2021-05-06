﻿// <copyright file="Metadata.cs" company="OpenTelemetry Authors">
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
