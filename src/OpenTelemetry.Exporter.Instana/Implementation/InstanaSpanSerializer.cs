// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Exporter.Instana.Implementation;

internal static class InstanaSpanSerializer
{
    private static readonly long UnixZeroTime =
#if NET
        DateTimeOffset.UnixEpoch.Ticks;
#else
        new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
#endif

    internal static void Serialize(InstanaSpan instanaSpan, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        AppendProperty(instanaSpan.T, "t", writer);
        AppendProperty(instanaSpan.S, "s", writer);

        if (!string.IsNullOrEmpty(instanaSpan.P))
        {
            AppendProperty(instanaSpan.P, "p", writer);
        }

        if (!string.IsNullOrEmpty(instanaSpan.Lt))
        {
            AppendProperty(instanaSpan.Lt, "lt", writer);
        }

        if (instanaSpan.Tp)
        {
            AppendProperty("true", "tp", writer);
        }

        if (instanaSpan.K != SpanKind.NOT_SET)
        {
            AppendProperty((((int)instanaSpan.K) + 1).ToString(CultureInfo.InvariantCulture), "k", writer);
        }

        AppendProperty(instanaSpan.N, "n", writer);

        writer.WritePropertyName("ts");
        writer.WriteNumberValue(DateToUnixMillis(instanaSpan.Ts));

        writer.WritePropertyName("d");
        writer.WriteNumberValue(instanaSpan.D / 10_000L);

        SerializeData(instanaSpan, writer);

        writer.WritePropertyName("f");
        writer.WriteStartObject();

        writer.WritePropertyName("e");
        writer.WriteStringValue(instanaSpan.F.E);

        writer.WriteEndObject();

        writer.WritePropertyName("ec");
        writer.WriteNumberValue(instanaSpan.Ec);

        writer.WriteEndObject();
    }

    private static long DateToUnixMillis(long timeStamp) => (timeStamp - UnixZeroTime) / 10_000;

    private static void SerializeTags(Dictionary<string, string>? tags, Utf8JsonWriter writer)
    {
        if (tags == null || tags.Count < 1)
        {
            return;
        }

        writer.WritePropertyName(InstanaExporterConstants.TagsField);
        writer.WriteStartObject();

        using var enumerator = tags.GetEnumerator();

        try
        {
            while (enumerator.MoveNext())
            {
                writer.WritePropertyName(enumerator.Current.Key);
                writer.WriteStringValue(enumerator.Current.Value);
            }
        }
        catch (InvalidOperationException)
        {
            // If the collection gets modified while serializing, we might get a collision.
            // There is no good way of preventing this and continuing normally except locking.
        }

        writer.WriteEndObject();
    }

    private static void AppendProperty(string? value, string name, Utf8JsonWriter json)
    {
        json.WritePropertyName(name);
        json.WriteStringValue(value);
    }

    private static void SerializeData(InstanaSpan instanaSpan, Utf8JsonWriter writer)
    {
        if (instanaSpan.Data.Values == null)
        {
            return;
        }

        writer.WritePropertyName("data");
        writer.WriteStartObject();

        using (var enumerator = instanaSpan.Data.Values.GetEnumerator())
        {
            try
            {
                while (enumerator.MoveNext())
                {
                    writer.WritePropertyName(enumerator.Current.Key);
                    writer.WriteStringValue(enumerator.Current.Value.ToString());
                }
            }
            catch (InvalidOperationException)
            {
                // If the collection gets modified while serializing, we might get a collision.
                // There is no good way of preventing this and continuing normally except locking.
            }
        }

        SerializeTags(instanaSpan.Data.Tags, writer);

        if (instanaSpan.Data.Events is { Count: > 0 } events)
        {
            writer.WritePropertyName(InstanaExporterConstants.EventsField);
            writer.WriteStartArray();

            SerializeEvents(events, writer);

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void SerializeEvents(List<SpanEvent> events, Utf8JsonWriter writer)
    {
        using var enumerator = events.GetEnumerator();

        try
        {
            while (enumerator.MoveNext())
            {
                writer.WriteStartObject();

                writer.WritePropertyName(InstanaExporterConstants.EventNameField);
                writer.WriteStringValue(enumerator.Current.Name);

                writer.WritePropertyName(InstanaExporterConstants.EventTimestampField);
                writer.WriteStringValue(DateToUnixMillis(enumerator.Current.Ts).ToString(CultureInfo.InvariantCulture));

                SerializeTags(enumerator.Current.Tags, writer);

                writer.WriteEndObject();
            }
        }
        catch (InvalidOperationException)
        {
            // If the collection gets modified while serializing, we might get a collision.
            // There is no good way of preventing this and continuing normally except locking.
        }
    }
}
