// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Serializes spans into rows matching the Collector exporter's <c>AdxTrace</c> schema.
/// </summary>
internal sealed class AdxTraceSerializer
{
    internal const string LegacyStatusCodeTag = "otel.status_code";
    internal const string LegacyStatusDescriptionTag = "otel.status_description";

    private readonly AttributeMap attributes = new();
    private readonly AttributeMap nested = new();

    public void Write(JsonRowWriter rows, Activity activity, AttributeMap resourceAttributes)
    {
        var writer = rows.BeginRow();
        try
        {
            this.WriteActivity(writer, activity, resourceAttributes);
            rows.EndRow();
        }
        catch (Exception ex)
        {
            rows.AbortRow();
            AzureDataExplorerExporterEventSource.Log.SerializationFailed("traces", ex);
        }
    }

    internal static (string StatusCode, string StatusMessage) GetStatus(Activity activity)
    {
        switch (activity.Status)
        {
            case ActivityStatusCode.Ok:
                return ("STATUS_CODE_OK", string.Empty);
            case ActivityStatusCode.Error:
                return ("STATUS_CODE_ERROR", activity.StatusDescription ?? string.Empty);
        }

        // Fall back to legacy status tags set by older instrumentation.
        var legacyCode = activity.GetTagItem(LegacyStatusCodeTag) as string;
        if (string.Equals(legacyCode, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return ("STATUS_CODE_ERROR", activity.GetTagItem(LegacyStatusDescriptionTag) as string ?? string.Empty);
        }

        if (string.Equals(legacyCode, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return ("STATUS_CODE_OK", string.Empty);
        }

        return ("STATUS_CODE_UNSET", string.Empty);
    }

    internal static string GetSpanKind(ActivityKind kind) => kind switch
    {
        ActivityKind.Internal => "SPAN_KIND_INTERNAL",
        ActivityKind.Server => "SPAN_KIND_SERVER",
        ActivityKind.Client => "SPAN_KIND_CLIENT",
        ActivityKind.Producer => "SPAN_KIND_PRODUCER",
        ActivityKind.Consumer => "SPAN_KIND_CONSUMER",
        _ => "SPAN_KIND_UNSPECIFIED",
    };

    private void WriteActivity(Utf8JsonWriter writer, Activity activity, AttributeMap resourceAttributes)
    {
        var (statusCode, statusMessage) = GetStatus(activity);

        writer.WriteStartObject();
        writer.WriteString("TraceID", AdxJson.FormatTraceId(activity.TraceId));
        writer.WriteString("SpanID", AdxJson.FormatSpanId(activity.SpanId));
        writer.WriteString("ParentID", AdxJson.FormatSpanId(activity.ParentSpanId));
        writer.WriteString("SpanName", activity.DisplayName);
        writer.WriteString("SpanStatus", statusCode);
        writer.WriteString("SpanStatusMessage", statusMessage);
        writer.WriteString("SpanKind", GetSpanKind(activity.Kind));
        writer.WriteString("StartTime", AdxJson.FormatTimestamp(activity.StartTimeUtc));
        writer.WriteString("EndTime", AdxJson.FormatTimestamp(activity.StartTimeUtc + activity.Duration));

        writer.WritePropertyName("ResourceAttributes");
        resourceAttributes.WriteTo(writer);

        this.attributes.Clear();
        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key == LegacyStatusCodeTag || tag.Key == LegacyStatusDescriptionTag)
            {
                continue;
            }

            this.attributes.Set(tag.Key, tag.Value);
        }

        this.attributes.SetScope(activity.Source.Name, activity.Source.Version);
        writer.WritePropertyName("TraceAttributes");
        this.attributes.WriteTo(writer);

        writer.WriteStartArray("Events");
        foreach (var activityEvent in activity.Events)
        {
            writer.WriteStartObject();
            writer.WriteString("EventName", activityEvent.Name);
            writer.WriteString("Timestamp", AdxJson.FormatTimestamp(activityEvent.Timestamp.UtcDateTime));
            this.nested.Clear();
            foreach (var tag in activityEvent.Tags)
            {
                this.nested.Set(tag.Key, tag.Value);
            }

            writer.WritePropertyName("EventAttributes");
            this.nested.WriteTo(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("Links");
        foreach (var link in activity.Links)
        {
            writer.WriteStartObject();
            writer.WriteString("TraceID", AdxJson.FormatTraceId(link.Context.TraceId));
            writer.WriteString("SpanID", AdxJson.FormatSpanId(link.Context.SpanId));
            writer.WriteString("TraceState", link.Context.TraceState ?? string.Empty);
            this.nested.Clear();
            if (link.Tags != null)
            {
                foreach (var tag in link.Tags)
                {
                    this.nested.Set(tag.Key, tag.Value);
                }
            }

            writer.WritePropertyName("SpanLinkAttributes");
            this.nested.WriteTo(writer);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
