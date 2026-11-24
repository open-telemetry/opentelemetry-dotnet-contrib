// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;

namespace OpenTelemetry.Instrumentation.AWS.Implementation;

/// <summary>
/// Serializes AWS SDK model objects to JSON using their PascalCase property names, which
/// match the AWS wire format, without referencing the service-specific SDK packages.
/// </summary>
internal static class AwsSdkModelJsonSerializer
{
    private const int MaxDepth = 16;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    /// <summary>
    /// Serializes <paramref name="value"/> to JSON.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON, or <see langword="null"/> if the value is <see langword="null"/> or serializes to an empty object or array.</returns>
    internal static string? Serialize(object? value)
    {
        if (value == null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, value, 0);
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        return json is "{}" or "[]" ? null : json;
    }

    private static void Write(Utf8JsonWriter writer, object? value, int depth)
    {
        if (value == null || depth > MaxDepth)
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
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                break;
            case Enum:
                writer.WriteStringValue(value.ToString());
                break;
            case int or long or short or byte or sbyte or ushort or uint or ulong:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case double or float or decimal:
                writer.WriteNumberValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                    Write(writer, entry.Value, depth + 1);
                }

                writer.WriteEndObject();
                break;
            case IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    Write(writer, item, depth + 1);
                }

                writer.WriteEndArray();
                break;
            default:
                WriteObject(writer, value, depth);
                break;
        }
    }

    private static void WriteObject(Utf8JsonWriter writer, object value, int depth)
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
            Write(writer, propertyValue, depth + 1);
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
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsSdkIsSetFlag(p))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray());

    // The SDK exposes flags such as AttributeValue.IsBOOLSet that are not part of the wire format.
    private static bool IsSdkIsSetFlag(PropertyInfo property)
        => property.PropertyType == typeof(bool)
        && property.Name.StartsWith("Is", StringComparison.Ordinal)
        && property.Name.EndsWith("Set", StringComparison.Ordinal);
}
