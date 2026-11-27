// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Serializes log records into rows matching the Collector exporter's <c>AdxLog</c> schema.
/// </summary>
internal sealed class AdxLogSerializer
{
    internal const string OriginalFormatKey = "{OriginalFormat}";
    internal const string EventIdKey = "event.id";
    internal const string EventNameKey = "event.name";
    internal const string ExceptionTypeKey = "exception.type";
    internal const string ExceptionMessageKey = "exception.message";
    internal const string ExceptionStacktraceKey = "exception.stacktrace";

    private readonly AttributeMap attributes = new();

    public void Write(JsonRowWriter rows, LogRecord logRecord, AttributeMap resourceAttributes)
    {
        var writer = rows.BeginRow();
        try
        {
            this.WriteLogRecord(writer, logRecord, resourceAttributes);
            rows.EndRow();
        }
        catch (Exception ex)
        {
            rows.AbortRow();
            AzureDataExplorerExporterEventSource.Log.SerializationFailed("logs", ex);
        }
    }

    internal static (string Text, int Number) GetSeverity(LogLevel logLevel) => logLevel switch
    {
        // Mapping matches the OpenTelemetry .NET SDK/OTLP exporter.
        LogLevel.Trace => ("Trace", 1),
        LogLevel.Debug => ("Debug", 5),
        LogLevel.Information => ("Info", 9),
        LogLevel.Warning => ("Warn", 13),
        LogLevel.Error => ("Error", 17),
        LogLevel.Critical => ("Fatal", 21),
        _ => (string.Empty, 0),
    };

    private void WriteLogRecord(Utf8JsonWriter writer, LogRecord logRecord, AttributeMap resourceAttributes)
    {
        var body = logRecord.FormattedMessage ?? logRecord.Body;

        this.attributes.Clear();
        if (logRecord.Attributes != null)
        {
            foreach (var attribute in logRecord.Attributes)
            {
                if (attribute.Key == OriginalFormatKey && body == null)
                {
                    body = attribute.Value as string;
                }

                this.attributes.Set(attribute.Key, attribute.Value);
            }
        }

        if (logRecord.EventId.Id != 0)
        {
            this.attributes.Set(EventIdKey, logRecord.EventId.Id);
        }

        if (!string.IsNullOrEmpty(logRecord.EventId.Name))
        {
            this.attributes.Set(EventNameKey, logRecord.EventId.Name);
        }

        if (logRecord.Exception != null)
        {
            this.attributes.Set(ExceptionTypeKey, logRecord.Exception.GetType().FullName);
            this.attributes.Set(ExceptionMessageKey, logRecord.Exception.Message);
            this.attributes.Set(ExceptionStacktraceKey, logRecord.Exception.ToInvariantString());
        }

        logRecord.ForEachScope(
            static (scope, map) =>
            {
                foreach (var item in scope)
                {
                    if (!string.IsNullOrEmpty(item.Key) && item.Key != OriginalFormatKey)
                    {
                        map.Set(item.Key, item.Value);
                    }
                }
            },
            this.attributes);

        this.attributes.SetScope(logRecord.CategoryName, null);

        var timestamp = AdxJson.FormatTimestamp(logRecord.Timestamp);

        writer.WriteStartObject();
        writer.WriteString("Timestamp", timestamp);

        // The .NET SDK does not expose a separate observed timestamp; the record
        // timestamp is captured when the log is emitted.
        writer.WriteString("ObservedTimestamp", timestamp);
        writer.WriteString("TraceID", AdxJson.FormatTraceId(logRecord.TraceId));
        writer.WriteString("SpanID", AdxJson.FormatSpanId(logRecord.SpanId));
        var (severityText, severityNumber) = GetSeverity(logRecord.LogLevel);
        writer.WriteString("SeverityText", severityText);
        writer.WriteNumber("SeverityNumber", severityNumber);
        writer.WriteString("Body", body ?? string.Empty);
        writer.WritePropertyName("ResourceAttributes");
        resourceAttributes.WriteTo(writer);
        writer.WritePropertyName("LogsAttributes");
        this.attributes.WriteTo(writer);
        writer.WriteEndObject();
    }
}
