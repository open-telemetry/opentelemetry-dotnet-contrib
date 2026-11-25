// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Serializes AWS SDK model objects to JSON using their PascalCase property names, which
/// match the AWS wire format.
/// </summary>
internal static class AWSModelJsonSerializer
{
    // The output is only used as span attribute values, so readable characters such as
    // non-ASCII letters, '+' and '&' are left unescaped.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    /// <summary>
    /// Serializes <paramref name="value"/> to JSON.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>
    /// The JSON, or <see langword="null"/> if the value is <see langword="null"/>
    /// or serializes to an empty object or array.
    /// </returns>
    internal static string? Serialize(object? value)
    {
        if (value == null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            Write(writer, value);
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        return json is "{}" or "[]" ? null : json;
    }

    private static void Write(Utf8JsonWriter writer, object? value)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case ConstantClass constant:
                writer.WriteStringValue(constant.Value);
                break;
            case MemoryStream memoryStream:
                writer.WriteBase64StringValue(memoryStream.ToArray());
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName((string)entry.Key);
                    Write(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                WriteObject(writer, value);
                break;
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, object value)
    {
        writer.WriteStartObject();
        foreach (var property in GetProperties(value.GetType()))
        {
            var propertyValue = property.GetValue(value);
            if (propertyValue == null || propertyValue is ICollection { Count: 0 })
            {
                continue;
            }

            writer.WritePropertyName(property.Name);
            Write(writer, propertyValue);
        }

        writer.WriteEndObject();
    }

#if NET
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "The reflected properties were already used by the AWS SDK's marshallers so the properties could not have been trimmed.")]
#endif
    private static PropertyInfo[] GetProperties(Type type)
        => PropertiesCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !IsSdkIsSetFlag(p))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray());

    // Flags such as AttributeValue.IsBOOLSet are SDK client-side state, absent from the wire format.
    private static bool IsSdkIsSetFlag(PropertyInfo property)
        => property.PropertyType == typeof(bool)
        && property.Name.StartsWith("Is", StringComparison.Ordinal)
        && property.Name.EndsWith("Set", StringComparison.Ordinal);
}
