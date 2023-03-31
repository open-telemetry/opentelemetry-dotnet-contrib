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
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class LogRecordCommonSchemaJsonSerializer : CommonSchemaJsonSerializer<LogRecord>
{
    private static readonly JsonEncodedText SeverityTextProperty = JsonEncodedText.Encode("severityText");
    private static readonly JsonEncodedText SeverityNumberProperty = JsonEncodedText.Encode("severityNumber");
    private static readonly JsonEncodedText BodyProperty = JsonEncodedText.Encode("body");
    private static readonly JsonEncodedText DistributedTraceExtensionProperty = JsonEncodedText.Encode("dt");
    private static readonly JsonEncodedText DistributedTraceExtensionTraceIdProperty = JsonEncodedText.Encode("traceId");
    private static readonly JsonEncodedText DistributedTraceExtensionSpanIdProperty = JsonEncodedText.Encode("spanId");
    private static readonly JsonEncodedText DistributedTraceExtensionTraceFlagsProperty = JsonEncodedText.Encode("traceFlags");
    private static readonly JsonEncodedText ExceptionExtensionProperty = JsonEncodedText.Encode("ex");
    private static readonly JsonEncodedText ExceptionExtensionTypeProperty = JsonEncodedText.Encode("type");
    private static readonly JsonEncodedText ExceptionExtensionMessageProperty = JsonEncodedText.Encode("msg");
    private static readonly JsonEncodedText ExceptionExtensionStackTraceProperty = JsonEncodedText.Encode("stack");

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

    private static readonly Action<LogRecordScope, CommonSchemaJsonSerializationState> SerializeScopeItemToJson = (scope, serializationState) =>
    {
        var writer = serializationState.Writer;

        foreach (KeyValuePair<string, object?> scopeAttribute in scope)
        {
            if (scopeAttribute.Key == "{OriginalFormat}")
            {
                return;
            }

            if (AttributeKeyStartWithExtensionPrefix(scopeAttribute.Key))
            {
                serializationState.AddExtensionAttribute(scopeAttribute!);
                continue;
            }

            CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(scopeAttribute.Key, scopeAttribute.Value, writer);
        }
    };

    private readonly EventNameManager eventNameManager;
    private readonly OneCollectorExporterSerializationExceptionStackTraceHandlingType exceptionStackTraceHandling;

    public LogRecordCommonSchemaJsonSerializer(
        EventNameManager eventNameManager,
        string tenantToken,
        OneCollectorExporterSerializationExceptionStackTraceHandlingType exceptionStackTraceHandling = OneCollectorExporterSerializationExceptionStackTraceHandlingType.Ignore,
        int maxPayloadSizeInBytes = int.MaxValue,
        int maxNumberOfItemsPerPayload = int.MaxValue)
        : base(tenantToken, maxPayloadSizeInBytes, maxNumberOfItemsPerPayload)
    {
        Debug.Assert(eventNameManager != null, "eventNameManager was null");

        this.eventNameManager = eventNameManager!;
        this.exceptionStackTraceHandling = exceptionStackTraceHandling;
    }

    public override string Description => "LogRecord Common Schema JSON";

    protected override void SerializeItemToJson(Resource resource, LogRecord item, CommonSchemaJsonSerializationState serializationState)
    {
        Debug.Assert(serializationState != null, "serializationState was null");

        var writer = serializationState!.Writer;

        Debug.Assert(writer != null, "writer was null");

        var eventName = this.eventNameManager.ResolveEventFullName(
            item.CategoryName,
            item.EventId.Name);

        writer!.WriteStartObject();

        writer.WriteString(CommonSchemaJsonSerializationHelper.VersionProperty, CommonSchemaJsonSerializationHelper.Version4Value);

        writer.WritePropertyName(CommonSchemaJsonSerializationHelper.NameProperty);
#if DEBUG
        writer.WriteRawValue(eventName, skipInputValidation: false);
#else
        writer.WriteRawValue(eventName, skipInputValidation: true);
#endif

        writer.WriteString(CommonSchemaJsonSerializationHelper.TimeProperty, item.Timestamp);

        writer.WriteString(CommonSchemaJsonSerializationHelper.IKeyProperty, this.TenantTokenWithTenancySystemSymbol);

        writer.WriteStartObject(CommonSchemaJsonSerializationHelper.DataProperty);

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

                if (AttributeKeyStartWithExtensionPrefix(attribute.Key))
                {
                    serializationState.AddExtensionAttribute(attribute);
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

        SerializeResourceToJsonInsideCurrentObject(resource, serializationState);

        item.ForEachScope(SerializeScopeItemToJson, serializationState);

        writer.WriteEndObject();

        this.SerializeExtensionPropertiesToJson(item, writer, serializationState);

        writer.WriteEndObject();
    }

    private void SerializeExtensionPropertiesToJson(LogRecord item, Utf8JsonWriter writer, CommonSchemaJsonSerializationState serializationState)
    {
        var hasTraceContext = item.TraceId != default;
        var hasException = item.Exception != null;

        if (!hasTraceContext && !hasException && serializationState.ExtensionAttributeCount <= 0)
        {
            return;
        }

        writer.WriteStartObject(CommonSchemaJsonSerializationHelper.ExtensionsProperty);

        if (hasTraceContext)
        {
            writer.WriteStartObject(DistributedTraceExtensionProperty);
            writer.WriteString(DistributedTraceExtensionTraceIdProperty, item.TraceId.ToHexString());
            writer.WriteString(DistributedTraceExtensionSpanIdProperty, item.SpanId.ToHexString());
            writer.WriteNumber(DistributedTraceExtensionTraceFlagsProperty, (int)item.TraceFlags);
            writer.WriteEndObject();
        }

        if (hasException)
        {
            writer.WriteStartObject(ExceptionExtensionProperty);
            writer.WriteString(ExceptionExtensionTypeProperty, item.Exception!.GetType().FullName);
            writer.WriteString(ExceptionExtensionMessageProperty, item.Exception.Message);

            if (this.exceptionStackTraceHandling == OneCollectorExporterSerializationExceptionStackTraceHandlingType.IncludeAsString)
            {
                writer.WriteString(ExceptionExtensionStackTraceProperty, item.Exception.ToInvariantString());
            }

            writer.WriteEndObject();
        }

        serializationState.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: false);

        writer.WriteEndObject();
    }
}
