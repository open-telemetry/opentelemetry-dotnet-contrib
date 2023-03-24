// <copyright file="ThreadStorageHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.OneCollector;

internal static class ThreadStorageHelper
{
    [ThreadStatic]
    private static Utf8JsonWriter? utf8JsonWriter;

    [ThreadStatic]
    private static CommonSchemaJsonSerializationState? commonSchemaJsonSerializationState;

    public static CommonSchemaJsonSerializationState GetCommonSchemaJsonSerializationState(string itemType, Stream stream)
    {
        var writer = utf8JsonWriter;
        if (writer == null)
        {
            writer = utf8JsonWriter = new(
                stream,
                new JsonWriterOptions { SkipValidation = true });
        }
        else
        {
            writer.Reset(stream);
        }

        var serializationState = commonSchemaJsonSerializationState;
        if (serializationState == null)
        {
            serializationState = commonSchemaJsonSerializationState ??= new(itemType, writer);
        }
        else
        {
            serializationState.Reset(itemType, writer);
        }

        return serializationState;
    }
}
