// <copyright file="CommonSchemaJsonSerializer.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

internal abstract class CommonSchemaJsonSerializer<T> : ISerializer<T>
    where T : class
{
#if NET6_0_OR_GREATER
    public const int MaximumStackAllocSizeInBytes = 256;
#endif

    protected static readonly JsonEncodedText VersionProperty = JsonEncodedText.Encode("ver");
    protected static readonly JsonEncodedText Version4Value = JsonEncodedText.Encode("4.0");
    protected static readonly JsonEncodedText NameProperty = JsonEncodedText.Encode("name");
    protected static readonly JsonEncodedText TimeProperty = JsonEncodedText.Encode("time");
    protected static readonly JsonEncodedText IKeyProperty = JsonEncodedText.Encode("iKey");
    protected static readonly JsonEncodedText DataProperty = JsonEncodedText.Encode("data");

    private const char OneCollectorTenancySymbol = 'o';

    private static readonly byte[] NewLine = "\n"u8.ToArray();
    [ThreadStatic]
    private static Utf8JsonWriter? threadWriter;

    private readonly int maxPayloadSizeInBytes;
    private readonly int maxNumberOfItemsPerPayload;

    protected CommonSchemaJsonSerializer(
        string tenantToken,
        int maxPayloadSizeInBytes = int.MaxValue,
        int maxNumberOfItemsPerPayload = int.MaxValue)
    {
        Debug.Assert(!string.IsNullOrEmpty(tenantToken), "tenantToken was null or empty.");

        this.maxPayloadSizeInBytes = maxPayloadSizeInBytes;
        this.maxNumberOfItemsPerPayload = maxNumberOfItemsPerPayload;

        this.TenantTokenWithTenancySystemSymbol = JsonEncodedText.Encode($"{OneCollectorTenancySymbol}:{tenantToken}");
    }

    public abstract string Description { get; }

    protected JsonEncodedText TenantTokenWithTenancySystemSymbol { get; }

    public void SerializeBatchOfItemsToStream(Resource resource, in Batch<T> batch, Stream stream, out BatchSerializationResult result)
    {
        Guard.ThrowIfNull(stream);

        var numberOfSerializedItems = 0;
        long payloadSizeInBytes = 0;

        var writer = threadWriter ??= new(
            stream,
            new JsonWriterOptions
            {
#if DEBUG
                SkipValidation = false,
#else
                SkipValidation = true,
#endif
            });

        foreach (var item in batch)
        {
            // Note: This is a slow operation. We call this each iteration
            // (instead of once per batch) to reset _currentDepth on
            // Utf8JsonWriter so it doesn't write a comma after each record.
            // Need a faster solution here!
            writer.Reset(stream);

            this.SerializeItemToJson(resource, item, writer);

            var currentItemSizeInBytes = writer.BytesCommitted - payloadSizeInBytes + writer.BytesPending + 1;

            payloadSizeInBytes += currentItemSizeInBytes;

            writer.Flush();

            stream.Write(NewLine, 0, 1);

            if (++numberOfSerializedItems >= this.maxNumberOfItemsPerPayload)
            {
                break;
            }

            if (payloadSizeInBytes >= this.maxPayloadSizeInBytes)
            {
                result = new BatchSerializationResult
                {
                    NumberOfItemsSerialized = numberOfSerializedItems,
                    PayloadSizeInBytes = payloadSizeInBytes,
                    PayloadOverflowItemSizeInBytes = currentItemSizeInBytes,
                };
                return;
            }
        }

        result = new BatchSerializationResult
        {
            NumberOfItemsSerialized = numberOfSerializedItems,
            PayloadSizeInBytes = payloadSizeInBytes,
        };
    }

    protected static void SerializeKeyValueToJson(string key, object? value, Utf8JsonWriter writer)
    {
        writer.WritePropertyName(key);
        SerializeValueToJson(value, writer);
    }

    protected static void SerializeValueToJson(object? value, Utf8JsonWriter writer)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (value)
        {
            case bool v:
                writer.WriteBooleanValue(v);
                return;

            case byte v:
                writer.WriteNumberValue(v);
                return;

            case sbyte v:
                writer.WriteNumberValue(v);
                return;

            case short v:
                writer.WriteNumberValue(v);
                return;

            case ushort v:
                writer.WriteNumberValue(v);
                return;

            case int v:
                writer.WriteNumberValue(v);
                return;

            case uint v:
                writer.WriteNumberValue(v);
                return;

            case long v:
                writer.WriteNumberValue(v);
                return;

            case ulong v:
                writer.WriteNumberValue(v);
                return;

            case float v:
                writer.WriteNumberValue(v);
                return;

            case double v:
                writer.WriteNumberValue(v);
                return;

            case decimal v:
                writer.WriteNumberValue(v);
                return;

            case string v:
                writer.WriteStringValue(v);
                return;

            case DateTime v:
                writer.WriteStringValue(v.ToUniversalTime());
                return;

            case DateTimeOffset v:
                writer.WriteStringValue(v);
                return;

#if NET7_0_OR_GREATER
            case DateOnly v:
                JsonMetadataServices.DateOnlyConverter.Write(writer, v, null!);
                return;
#endif

            case TimeSpan v:
                JsonMetadataServices.TimeSpanConverter.Write(writer, v, null!);
                return;

#if NET7_0_OR_GREATER
            case TimeOnly v:
                JsonMetadataServices.TimeOnlyConverter.Write(writer, v, null!);
                return;
#endif

            case Guid v:
                writer.WriteStringValue(v);
                return;

            case Uri v:
                JsonMetadataServices.UriConverter.Write(writer, v, null!);
                return;

            case Version v:
                JsonMetadataServices.VersionConverter.Write(writer, v, null!);
                return;

            case byte[] v:
                writer.WriteBase64StringValue(v);
                return;

            case Memory<byte> v:
                writer.WriteBase64StringValue(v.Span);
                return;

            case ArraySegment<byte> v:
                writer.WriteBase64StringValue(v);
                return;

            case Array v:
                SerializeArrayValueToJson(v, writer);
                return;

            case IEnumerable<KeyValuePair<string, object?>> v:
                SerializeMapValueToJson(v, writer);
                return;

            default:
                SerializeObjectValueToJson(value, writer);
                return;
        }
    }

    protected abstract void SerializeItemToJson(Resource resource, T item, Utf8JsonWriter writer);

    private static void SerializeArrayValueToJson(Array value, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();

        foreach (var element in value)
        {
            SerializeValueToJson(element, writer);
        }

        writer.WriteEndArray();
    }

    private static void SerializeMapValueToJson(IEnumerable<KeyValuePair<string, object?>> value, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        foreach (var element in value)
        {
            if (string.IsNullOrEmpty(element.Key))
            {
                continue;
            }

            SerializeKeyValueToJson(element.Key, element.Value, writer);
        }

        writer.WriteEndObject();
    }

    private static void SerializeObjectValueToJson(object value, Utf8JsonWriter writer)
    {
#if NET6_0_OR_GREATER
        if (value is ISpanFormattable spanFormattable)
        {
            Span<char> destination = stackalloc char[MaximumStackAllocSizeInBytes / 2];
            if (spanFormattable.TryFormat(destination, out int charsWritten, string.Empty, CultureInfo.InvariantCulture))
            {
                writer.WriteStringValue(destination.Slice(0, charsWritten));
                return;
            }
        }
#endif

        string v;

        try
        {
            v = Convert.ToString(value, CultureInfo.InvariantCulture)!;
        }
        catch
        {
            v = $"ERROR: type {value.GetType().FullName} is not supported";
        }

        writer.WriteStringValue(v);
    }
}
