// <copyright file="LogRecordCommonSchemaJsonSerializer.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class LogRecordCommonSchemaJsonSerializer : CommonSchemaJsonSerializer<LogRecord>
{
    private static readonly JsonEncodedText SeverityTextProperty = JsonEncodedText.Encode("severityText");
    private static readonly JsonEncodedText SeverityNumberProperty = JsonEncodedText.Encode("severityNumber");
    private static readonly JsonEncodedText BodyProperty = JsonEncodedText.Encode("body");

    private static readonly JsonEncodedText[] LogLevelToSeverityTextMappings = new JsonEncodedText[]
    {
        JsonEncodedText.Encode("Trace"),
        JsonEncodedText.Encode("Debug"),
        JsonEncodedText.Encode("Information"),
        JsonEncodedText.Encode("Warning"),
        JsonEncodedText.Encode("Error"),
        JsonEncodedText.Encode("Critical"),
        JsonEncodedText.Encode("Trace"), // Note: This is the "None" bucket.
    };

    private static readonly int[] LogLevelToSeverityNumberMappings = new int[]
    {
        1, 5, 9, 13, 17, 21, 1,
    };

    private static readonly Action<LogRecordScope, Utf8JsonWriter> SerializeScopeItemToJson = (s, w) =>
    {
        foreach (KeyValuePair<string, object?> scopeAttribute in s)
        {
            if (scopeAttribute.Key == "{OriginalFormat}")
            {
                return;
            }

            CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(scopeAttribute.Key, scopeAttribute.Value, w);
        }
    };

    private readonly EventNameManager eventNameManager;

    public LogRecordCommonSchemaJsonSerializer(
        EventNameManager eventNameManager,
        string tenantToken,
        int maxPayloadSizeInBytes = int.MaxValue,
        int maxNumberOfItemsPerPayload = int.MaxValue)
        : base(tenantToken, maxPayloadSizeInBytes, maxNumberOfItemsPerPayload)
    {
        Debug.Assert(eventNameManager != null, "eventNameManager was null");

        this.eventNameManager = eventNameManager!;
    }

    public override string Description => "LogRecord Common Schema JSON";

    protected override void SerializeItemToJson(Resource resource, LogRecord item, Utf8JsonWriter writer)
    {
        var eventName = this.eventNameManager.ResolveEventFullName(
            item.CategoryName,
            item.EventId.Name);

        writer.WriteStartObject();

        writer.WriteString(VersionProperty, Version4Value);

        writer.WritePropertyName(NameProperty);
#if DEBUG
        writer.WriteRawValue(eventName, skipInputValidation: false);
#else
        writer.WriteRawValue(eventName, skipInputValidation: true);
#endif

        writer.WriteString(TimeProperty, item.Timestamp);

        writer.WriteString(IKeyProperty, this.TenantTokenWithTenancySystemSymbol);

        writer.WriteStartObject(DataProperty);

        /* TODO: There doesn't seem to be a spot in common schema defined for
        event.id so we will drop for now.

        if (item.EventId.Id != 0)
        {
            writer.WriteNumber(EventIdProperty, item.EventId.Id);
        }*/

        var logLevel = (int)item.LogLevel;
        writer.WriteString(SeverityTextProperty, LogLevelToSeverityTextMappings[logLevel]);
        writer.WriteNumber(SeverityNumberProperty, LogLevelToSeverityNumberMappings[logLevel]);

        string? body = null;

        if (item.StateValues != null)
        {
            for (int i = 0; i < item.StateValues.Count; i++)
            {
                var attribute = item.StateValues[i];

                if (string.IsNullOrEmpty(attribute.Key))
                {
                    continue;
                }

                if (attribute.Key == "{OriginalFormat}")
                {
                    body = attribute.Value as string;
                    continue;
                }

                CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(attribute.Key, attribute.Value, writer);
            }
        }

        if (!string.IsNullOrEmpty(body))
        {
            writer.WriteString(BodyProperty, body);
        }
        else if (!string.IsNullOrEmpty(item.FormattedMessage))
        {
            writer.WriteString(BodyProperty, item.FormattedMessage);
        }

        if (resource.Attributes is IReadOnlyList<KeyValuePair<string, object?>> resourceAttributeList)
        {
            for (int i = 0; i < resourceAttributeList.Count; i++)
            {
                var resourceAttribute = resourceAttributeList[i];

                CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(resourceAttribute.Key, resourceAttribute.Value, writer);
            }
        }
        else
        {
            foreach (KeyValuePair<string, object> resourceAttribute in resource.Attributes)
            {
                CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(resourceAttribute.Key, resourceAttribute.Value, writer);
            }
        }

        item.ForEachScope(SerializeScopeItemToJson, writer);

        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
