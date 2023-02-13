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
using System.Text.Json;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

internal abstract class CommonSchemaJsonSerializer<T> : ISerializer<T>
    where T : class
{
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

    protected abstract void SerializeItemToJson(Resource resource, T item, Utf8JsonWriter writer);
}
