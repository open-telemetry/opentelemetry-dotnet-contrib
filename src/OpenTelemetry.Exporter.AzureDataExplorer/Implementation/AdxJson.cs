// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// JSON helpers producing values compatible with the OpenTelemetry Collector
/// Azure Data Explorer exporter.
/// </summary>
internal static class AdxJson
{
    internal const string ScopeNameKey = "scope.name";
    internal const string ScopeVersionKey = "scope.version";

    private const int MaxDepth = 8;
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary>
    /// Formats a timestamp as RFC 3339 in UTC with trailing fractional zeros
    /// trimmed, matching Go's <c>time.RFC3339Nano</c> layout (at 100ns precision).
    /// </summary>
    /// <param name="timestamp">Timestamp.</param>
    /// <returns>Formatted timestamp.</returns>
    public static string FormatTimestamp(DateTime timestamp)
    {
        var utc = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
        };

        return utc.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatTraceId(ActivityTraceId traceId)
        => traceId == default ? string.Empty : traceId.ToHexString();

    public static string FormatSpanId(ActivitySpanId spanId)
        => spanId == default ? string.Empty : spanId.ToHexString();

    /// <summary>
    /// Formats a double the same way as Go's <c>strconv.FormatFloat(f, 'g', -1, 64)</c>.
    /// </summary>
    /// <param name="value">Value.</param>
    /// <returns>Formatted value.</returns>
    public static string FormatGoFloat(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "+Inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Inf";
        }

        if (value == 0)
        {
            return 1 / value < 0 ? "-0" : "0";
        }

        var negative = value < 0;

        // "R" gives the shortest round-trippable representation on modern .NET.
        var text = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);

        var exponentAdjust = 0;
        var exponentIndex = text.IndexOfAny(['E', 'e']);
        if (exponentIndex >= 0)
        {
#pragma warning disable CA1846 // Prefer 'AsSpan' over 'Substring' (not available on all target frameworks)
            exponentAdjust = int.Parse(text.Substring(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
#pragma warning restore CA1846
            text = text.Substring(0, exponentIndex);
        }

        var dotIndex = text.IndexOfAny(['.']);
        var integerPart = dotIndex >= 0 ? text.Substring(0, dotIndex) : text;
        var fractionPart = dotIndex >= 0 ? text.Substring(dotIndex + 1) : string.Empty;

        var allDigits = integerPart + fractionPart;
        var decimalPoint = integerPart.Length + exponentAdjust;

        var leadingZeros = 0;
        while (leadingZeros < allDigits.Length - 1 && allDigits[leadingZeros] == '0')
        {
            leadingZeros++;
        }

        var digits = allDigits.Substring(leadingZeros).TrimEnd('0');
        decimalPoint -= leadingZeros;
        if (digits.Length == 0)
        {
            digits = "0";
        }

        var builder = new StringBuilder(32);
        if (negative)
        {
            builder.Append('-');
        }

        // Go uses %e when the exponent is < -4 or >= 6 for the shortest representation.
        var exponent = decimalPoint - 1;
        if (exponent < -4 || exponent >= 6)
        {
            builder.Append(digits[0]);
            if (digits.Length > 1)
            {
                builder.Append('.').Append(digits, 1, digits.Length - 1);
            }

            builder.Append('e').Append(exponent < 0 ? '-' : '+');
            var absExponent = Math.Abs(exponent);
            if (absExponent < 10)
            {
                builder.Append('0');
            }

            builder.Append(absExponent.ToString(CultureInfo.InvariantCulture));
        }
        else if (decimalPoint <= 0)
        {
            builder.Append("0.").Append('0', -decimalPoint).Append(digits);
        }
        else if (decimalPoint >= digits.Length)
        {
            builder.Append(digits).Append('0', decimalPoint - digits.Length);
        }
        else
        {
            builder.Append(digits, 0, decimalPoint).Append('.').Append(digits, decimalPoint, digits.Length - decimalPoint);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes a double, emitting <c>"NaN"</c>, <c>"Infinity"</c> or
    /// <c>"-Infinity"</c> strings for non-finite values.
    /// </summary>
    /// <param name="writer">Writer.</param>
    /// <param name="value">Value.</param>
    public static void WriteSafeDouble(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value))
        {
            writer.WriteStringValue("NaN");
        }
        else if (double.IsPositiveInfinity(value))
        {
            writer.WriteStringValue("Infinity");
        }
        else if (double.IsNegativeInfinity(value))
        {
            writer.WriteStringValue("-Infinity");
        }
        else
        {
            writer.WriteNumberValue(value);
        }
    }

    public static void WriteValue(Utf8JsonWriter writer, object? value)
        => WriteValue(writer, value, depth: 0);

    private static void WriteValue(Utf8JsonWriter writer, object? value, int depth)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string s:
                writer.WriteStringValue(s);
                return;
            case bool b:
                writer.WriteBooleanValue(b);
                return;
            case char c:
                writer.WriteStringValue(c.ToString());
                return;
            case sbyte i8:
                writer.WriteNumberValue(i8);
                return;
            case byte u8:
                writer.WriteNumberValue(u8);
                return;
            case short i16:
                writer.WriteNumberValue(i16);
                return;
            case ushort u16:
                writer.WriteNumberValue(u16);
                return;
            case int i32:
                writer.WriteNumberValue(i32);
                return;
            case uint u32:
                writer.WriteNumberValue(u32);
                return;
            case long i64:
                writer.WriteNumberValue(i64);
                return;
            case ulong u64:
                writer.WriteNumberValue(u64);
                return;
            case float f:
                WriteSafeDouble(writer, f);
                return;
            case double d:
                WriteSafeDouble(writer, d);
                return;
            case decimal m:
                writer.WriteNumberValue(m);
                return;
            case DateTime dt:
                writer.WriteStringValue(FormatTimestamp(dt));
                return;
            case DateTimeOffset dto:
                writer.WriteStringValue(FormatTimestamp(dto.UtcDateTime));
                return;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                return;
        }

        if (depth >= MaxDepth)
        {
            writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
            return;
        }

        if (value is IEnumerable<KeyValuePair<string, object?>> map)
        {
            writer.WriteStartObject();
            foreach (var item in map)
            {
                writer.WritePropertyName(item.Key);
                WriteValue(writer, item.Value, depth + 1);
            }

            writer.WriteEndObject();
            return;
        }

        if (value is IEnumerable enumerable)
        {
            writer.WriteStartArray();
            foreach (var item in enumerable)
            {
                WriteValue(writer, item, depth + 1);
            }

            writer.WriteEndArray();
            return;
        }

        string? text;
        try
        {
            text = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            text = $"ERROR: type {value.GetType().FullName} is not supported";
        }

        writer.WriteStringValue(text);
    }
}
