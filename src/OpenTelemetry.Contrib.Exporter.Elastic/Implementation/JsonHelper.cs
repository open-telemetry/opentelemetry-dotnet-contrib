// <copyright file="JsonHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Contrib.Exporter.Elastic.Implementation
{
    internal static class JsonHelper
    {
        internal static readonly JsonEncodedText TransactionPropertyName = JsonEncodedText.Encode("transaction");
        internal static readonly JsonEncodedText SpanPropertyName = JsonEncodedText.Encode("span");

        internal static readonly JsonEncodedText NamePropertyName = JsonEncodedText.Encode("name");
        internal static readonly JsonEncodedText TraceIdPropertyName = JsonEncodedText.Encode("trace_id");
        internal static readonly JsonEncodedText IdPropertyName = JsonEncodedText.Encode("id");
        internal static readonly JsonEncodedText ParentIdPropertyName = JsonEncodedText.Encode("parent_id");
        internal static readonly JsonEncodedText DurationPropertyName = JsonEncodedText.Encode("duration");
        internal static readonly JsonEncodedText TimestampPropertyName = JsonEncodedText.Encode("timestamp");
        internal static readonly JsonEncodedText TypePropertyName = JsonEncodedText.Encode("type");
        internal static readonly JsonEncodedText OutcomePropertyName = JsonEncodedText.Encode("outcome");
        internal static readonly JsonEncodedText ResultPropertyName = JsonEncodedText.Encode("result");
        internal static readonly JsonEncodedText SpanCountPropertyName = JsonEncodedText.Encode("span_count");
        internal static readonly JsonEncodedText DroppedPropertyName = JsonEncodedText.Encode("dropped");
        internal static readonly JsonEncodedText StartedPropertyName = JsonEncodedText.Encode("started");

        internal static readonly JsonEncodedText MetadataPropertyName = JsonEncodedText.Encode("metadata");
        internal static readonly JsonEncodedText ServicePropertyName = JsonEncodedText.Encode("service");
        internal static readonly JsonEncodedText EnvironmentPropertyName = JsonEncodedText.Encode("environment");
        internal static readonly JsonEncodedText AgentPropertyName = JsonEncodedText.Encode("agent");
        internal static readonly JsonEncodedText VersionPropertyName = JsonEncodedText.Encode("version");
    }
}
