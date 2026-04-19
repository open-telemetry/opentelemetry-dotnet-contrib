// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;

namespace OpenTelemetry.Exporter.Instana.Implementation;

// TODO Use a proper JSON serializer that encodes strings safely.

internal static class InstanaSpanSerializer
{
    private const string Comma = ",";
    private const string OpenCurlyBrace = "{";
    private const string CloseCurlyBrace = "}";
    private const string Quote = "\"";
    private const string Colon = ":";
    private const string QuoteColon = Quote + Colon;
    private const string QuoteColonQuote = Quote + Colon + Quote;
    private const string QuoteCommaQuote = Quote + Comma + Quote;

    private static readonly long UnixZeroTime =
#if NET
        DateTimeOffset.UnixEpoch.Ticks;
#else
        new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
#endif

    internal static IEnumerator? GetSpanTagsEnumerator(InstanaSpan instanaSpan) => instanaSpan.Data.Tags.GetEnumerator();

    internal static IEnumerator? GetSpanEventsEnumerator(InstanaSpan instanaSpan) => instanaSpan.Data.Events.GetEnumerator();

    internal static void SerializeToStreamWriter(InstanaSpan instanaSpan, StreamWriter writer)
    {
        writer.Write(OpenCurlyBrace);
        AppendProperty(instanaSpan.T, "t", writer);
        writer.Write(Comma);
        AppendProperty(instanaSpan.S, "s", writer);
        writer.Write(Comma);

        if (!string.IsNullOrEmpty(instanaSpan.P))
        {
            AppendProperty(instanaSpan.P, "p", writer);
            writer.Write(Comma);
        }

        if (!string.IsNullOrEmpty(instanaSpan.Lt))
        {
            AppendProperty(instanaSpan.Lt, "lt", writer);
            writer.Write(Comma);
        }

        if (instanaSpan.Tp)
        {
            AppendProperty("true", "tp", writer);
            writer.Write(Comma);
        }

        if (instanaSpan.K != SpanKind.NOT_SET)
        {
            AppendProperty((((int)instanaSpan.K) + 1).ToString(CultureInfo.InvariantCulture), "k", writer);
            writer.Write(Comma);
        }

        AppendProperty(instanaSpan.N, "n", writer);
        writer.Write(Comma);
        AppendProperty(DateToUnixMillis(instanaSpan.Ts), "ts", writer);
        writer.Write(Comma);
        AppendProperty(instanaSpan.D / 10_000L, "d", writer);
        writer.Write(Comma);
        AppendObject(SerializeData, "data", instanaSpan, writer);
        writer.Write(Comma);
        AppendObject(SerializeFrom, "f", instanaSpan, writer);
        writer.Write(Comma);
        AppendProperty(instanaSpan.Ec, "ec", writer);
        writer.Write(CloseCurlyBrace);
    }

    private static void SerializeFrom(InstanaSpan instanaSpan, StreamWriter writer)
    {
        writer.Write("{\"e\":\"");
        writer.Write(instanaSpan.F.E);
        writer.Write("\"}");
    }

    private static long DateToUnixMillis(long timeStamp) => (timeStamp - UnixZeroTime) / 10_000;

    private static void SerializeTags(InstanaSpan instanaSpan, StreamWriter writer) =>
        SerializeTagsLogic(instanaSpan.Data.Tags, writer);

    private static void SerializeTagsLogic(Dictionary<string, string>? tags, StreamWriter writer)
    {
        writer.Write(OpenCurlyBrace);
        if (tags == null)
        {
            return;
        }

        using (var enumerator = tags.GetEnumerator())
        {
            byte i = 0;
            try
            {
                while (enumerator.MoveNext())
                {
                    if (i > 0)
                    {
                        writer.Write(Comma);
                    }
                    else
                    {
                        i = 1;
                    }

                    writer.Write(Quote);
                    writer.Write(enumerator.Current.Key);
                    writer.Write(QuoteColonQuote);
                    writer.Write(enumerator.Current.Value);
                    writer.Write(Quote);
                }
            }
            catch (InvalidOperationException)
            {
                // if the collection gets modified while serializing, we might get a collision.
                // There is no good way of preventing this and continuing normally except locking
                // which needs investigation
            }
        }

        writer.Write(CloseCurlyBrace);
    }

    private static void AppendProperty(string? value, string? name, StreamWriter json)
    {
        json.Write(Quote);
        json.Write(name);
        json.Write(QuoteColonQuote);
        json.Write(value);
        json.Write(Quote);
    }

    private static void AppendProperty(long value, string name, StreamWriter json)
    {
        json.Write(Quote);
        json.Write(name);
        json.Write(QuoteColon);
        json.Write(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendObject(Action<InstanaSpan, StreamWriter> valueFunction, string name, InstanaSpan instanaSpan, StreamWriter json)
    {
        json.Write(Quote);
        json.Write(name);
        json.Write(QuoteColon);
        valueFunction(instanaSpan, json);
    }

    private static void SerializeData(InstanaSpan instanaSpan, StreamWriter writer)
    {
        writer.Write(OpenCurlyBrace);
        if (instanaSpan.Data.Values == null)
        {
            return;
        }

        using (var enumerator = instanaSpan.Data.Values.GetEnumerator())
        {
            byte i = 0;
            try
            {
                while (enumerator.MoveNext())
                {
                    if (i > 0)
                    {
                        writer.Write(Comma);
                    }
                    else
                    {
                        i = 1;
                    }

                    writer.Write(Quote);
                    writer.Write(enumerator.Current.Key);
                    writer.Write(QuoteColonQuote);
                    writer.Write(enumerator.Current.Value.ToString());
                    writer.Write(Quote);
                }
            }
            catch (InvalidOperationException)
            {
                // If the collection gets modified while serializing, we might get a collision.
                // There is no good way of preventing this and continuing normally except locking.
            }
        }

        if (instanaSpan.Data.Tags.Count > 0)
        {
            writer.Write(Comma);

            // Serialize tags
            AppendObject(SerializeTags, InstanaExporterConstants.TagsField, instanaSpan, writer);
        }

        if (instanaSpan.Data.Events.Count > 0)
        {
            writer.Write(Comma);

            // Serialize events
            AppendObject(SerializeEvents, InstanaExporterConstants.EventsField, instanaSpan, writer);
        }

        writer.Write(CloseCurlyBrace);
    }

    private static void SerializeEvents(InstanaSpan instanaSpan, StreamWriter writer)
    {
        if (instanaSpan.Data.Events == null)
        {
            return;
        }

        using var enumerator = instanaSpan.Data.Events.GetEnumerator();
        byte i = 0;
        try
        {
            writer.Write("[");
            while (enumerator.MoveNext())
            {
                if (i > 0)
                {
                    writer.Write(Comma);
                }
                else
                {
                    i = 1;
                }

                writer.Write(OpenCurlyBrace);
                writer.Write(Quote);
                writer.Write(InstanaExporterConstants.EventNameField);
                writer.Write(QuoteColonQuote);
                writer.Write(enumerator.Current.Name);
                writer.Write(QuoteCommaQuote);
                writer.Write(InstanaExporterConstants.EventTimestampField);
                writer.Write(QuoteColonQuote);
                writer.Write(DateToUnixMillis(enumerator.Current.Ts).ToString(CultureInfo.InvariantCulture));
                writer.Write(Quote);

                if (enumerator.Current.Tags.Count > 0)
                {
                    writer.Write(Comma);
                    writer.Write(Quote);
                    writer.Write(InstanaExporterConstants.TagsField);
                    writer.Write(Quote);
                    writer.Write(Colon);
                    SerializeTagsLogic(enumerator.Current.Tags, writer);
                }

                writer.Write(CloseCurlyBrace);
            }

            writer.Write("]");
        }
        catch (InvalidOperationException)
        {
            // If the collection gets modified while serializing, we might get a collision.
            // There is no good way of preventing this and continuing normally except locking.
        }
    }
}
