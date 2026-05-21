// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SemanticConventions.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Minimal recursive-descent JSON reader sufficient for the embedded resolved-registry
/// shape. Avoids taking a runtime dependency on <c>System.Text.Json</c> in the analyzer DLL
/// (IDE-version clash risk; see <c>RegistryLoader</c>).
/// </summary>
internal abstract record JsonValue;

internal sealed record JsonNull : JsonValue
{
    public static readonly JsonNull Instance = new();
}

internal sealed record JsonBool(bool Value) : JsonValue;

internal sealed record JsonString(string Value) : JsonValue;

internal sealed record JsonNumber(string Raw) : JsonValue
{
    public double AsDouble => double.Parse(Raw, CultureInfo.InvariantCulture);
    public long AsLong => long.Parse(Raw, CultureInfo.InvariantCulture);
}

internal sealed record JsonArray(IReadOnlyList<JsonValue> Items) : JsonValue;

internal sealed record JsonObject(IReadOnlyDictionary<string, JsonValue> Members) : JsonValue
{
    public JsonValue? TryGet(string key) => Members.TryGetValue(key, out var v) ? v : null;

    public string GetString(string key)
        => TryGet(key) is JsonString s ? s.Value : string.Empty;

    public JsonArray? TryGetArray(string key)
        => TryGet(key) as JsonArray;
}

internal static class JsonReader
{
    public static JsonValue Parse(string text)
    {
        var index = 0;
        SkipWhitespace(text, ref index);
        var value = ReadValue(text, ref index);
        SkipWhitespace(text, ref index);
        if (index != text.Length)
            throw new FormatException($"Unexpected trailing content at position {index}.");
        return value;
    }

    private static JsonValue ReadValue(string text, ref int index)
    {
        SkipWhitespace(text, ref index);
        if (index >= text.Length)
            throw new FormatException("Unexpected end of input.");

        var c = text[index];
        return c switch
        {
            '{' => ReadObject(text, ref index),
            '[' => ReadArray(text, ref index),
            '"' => new JsonString(ReadString(text, ref index)),
            't' or 'f' => ReadBool(text, ref index),
            'n' => ReadNull(text, ref index),
            _ => ReadNumber(text, ref index)
        };
    }

    private static JsonObject ReadObject(string text, ref int index)
    {
        Expect(text, ref index, '{');
        var members = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        SkipWhitespace(text, ref index);

        if (index < text.Length && text[index] == '}')
        {
            index++;
            return new JsonObject(members);
        }

        while (true)
        {
            SkipWhitespace(text, ref index);
            var key = ReadString(text, ref index);
            SkipWhitespace(text, ref index);
            Expect(text, ref index, ':');
            var value = ReadValue(text, ref index);
            members[key] = value;
            SkipWhitespace(text, ref index);

            if (index >= text.Length)
                throw new FormatException("Unexpected end of input inside object.");

            if (text[index] == ',') { index++; continue; }
            if (text[index] == '}') { index++; break; }

            throw new FormatException($"Expected ',' or '}}' at position {index}, got '{text[index]}'.");
        }

        return new JsonObject(members);
    }

    private static JsonArray ReadArray(string text, ref int index)
    {
        Expect(text, ref index, '[');
        var items = new List<JsonValue>();
        SkipWhitespace(text, ref index);

        if (index < text.Length && text[index] == ']')
        {
            index++;
            return new JsonArray(items);
        }

        while (true)
        {
            var value = ReadValue(text, ref index);
            items.Add(value);
            SkipWhitespace(text, ref index);

            if (index >= text.Length)
                throw new FormatException("Unexpected end of input inside array.");

            if (text[index] == ',') { index++; continue; }
            if (text[index] == ']') { index++; break; }

            throw new FormatException($"Expected ',' or ']' at position {index}, got '{text[index]}'.");
        }

        return new JsonArray(items);
    }

    private static string ReadString(string text, ref int index)
    {
        Expect(text, ref index, '"');
        var builder = new StringBuilder();

        while (index < text.Length)
        {
            var c = text[index++];
            if (c == '"') return builder.ToString();

            if (c == '\\')
            {
                if (index >= text.Length)
                    throw new FormatException("Unterminated escape sequence.");
                var esc = text[index++];
                switch (esc)
                {
                    case '"':  builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/':  builder.Append('/'); break;
                    case 'b':  builder.Append('\b'); break;
                    case 'f':  builder.Append('\f'); break;
                    case 'n':  builder.Append('\n'); break;
                    case 'r':  builder.Append('\r'); break;
                    case 't':  builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length)
                            throw new FormatException("Truncated \\uXXXX escape.");
                        var hex = text.Substring(index, 4);
                        builder.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default:
                        throw new FormatException($"Unknown escape '\\{esc}' at position {index - 1}.");
                }
            }
            else
            {
                builder.Append(c);
            }
        }

        throw new FormatException("Unterminated string literal.");
    }

    private static JsonBool ReadBool(string text, ref int index)
    {
        if (text[index] == 't' && Matches(text, index, "true"))
        {
            index += 4;
            return new JsonBool(true);
        }
        if (text[index] == 'f' && Matches(text, index, "false"))
        {
            index += 5;
            return new JsonBool(false);
        }
        throw new FormatException($"Invalid boolean literal at position {index}.");
    }

    private static JsonNull ReadNull(string text, ref int index)
    {
        if (Matches(text, index, "null"))
        {
            index += 4;
            return JsonNull.Instance;
        }
        throw new FormatException($"Invalid null literal at position {index}.");
    }

    private static JsonNumber ReadNumber(string text, ref int index)
    {
        var start = index;
        if (text[index] == '-') index++;
        while (index < text.Length && IsNumberChar(text[index])) index++;
        return new JsonNumber(text.Substring(start, index - start));
    }

    private static bool IsNumberChar(char c)
        => c is >= '0' and <= '9' or '.' or 'e' or 'E' or '+' or '-';

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
    }

    private static void Expect(string text, ref int index, char expected)
    {
        if (index >= text.Length || text[index] != expected)
            throw new FormatException($"Expected '{expected}' at position {index}.");
        index++;
    }

    private static bool Matches(string text, int index, string token)
    {
        if (index + token.Length > text.Length) return false;
        for (var i = 0; i < token.Length; i++)
        {
            if (text[index + i] != token[i]) return false;
        }
        return true;
    }
}
