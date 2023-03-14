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
    private readonly int maxPayloadSizeInBytes;
    private readonly int maxNumberOfItemsPerPayload;
    private readonly string itemType = typeof(T).Name;

    protected CommonSchemaJsonSerializer(
        string tenantToken,
        int maxPayloadSizeInBytes = int.MaxValue,
        int maxNumberOfItemsPerPayload = int.MaxValue)
    {
        Debug.Assert(!string.IsNullOrEmpty(tenantToken), "tenantToken was null or empty.");

        this.maxPayloadSizeInBytes = maxPayloadSizeInBytes;
        this.maxNumberOfItemsPerPayload = maxNumberOfItemsPerPayload;

        this.TenantTokenWithTenancySystemSymbol = JsonEncodedText.Encode($"{CommonSchemaJsonSerializationHelper.OneCollectorTenancySymbol}:{tenantToken}");
    }

    public abstract string Description { get; }

    public OneCollectorExporterSerializationFormatType SerializationFormat => OneCollectorExporterSerializationFormatType.CommonSchemaV4JsonStream;

    protected JsonEncodedText TenantTokenWithTenancySystemSymbol { get; }

    public void SerializeBatchOfItemsToStream(Resource resource, in Batch<T> batch, Stream stream, int initialSizeOfPayloadInBytes, out BatchSerializationResult result)
    {
        Guard.ThrowIfNull(stream);

        var numberOfSerializedItems = 0;
        long payloadSizeInBytes = initialSizeOfPayloadInBytes;

        var state = ThreadStorageHelper.GetCommonSchemaJsonSerializationState(this.itemType, stream);

        var writer = state.Writer;

        foreach (var item in batch)
        {
            this.SerializeItemToJson(resource, item, state);

            var currentItemSizeInBytes = writer.BytesCommitted + writer.BytesPending + 1;

            payloadSizeInBytes += currentItemSizeInBytes;

            writer.Flush();
            writer.Reset();

            stream.Write(CommonSchemaJsonSerializationHelper.NewLine, 0, 1);

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

    protected static bool AttributeKeyStartWithExtensionPrefix(string attributeKey)
    {
        return attributeKey.StartsWith("ext.", StringComparison.OrdinalIgnoreCase);
    }

    protected static void SerializeResourceToJsonInsideCurrentObject(Resource resource, CommonSchemaJsonSerializationState serializationState)
    {
        Debug.Assert(resource != null, "resource was null");
        Debug.Assert(serializationState != null, "serializationState was null");

        var writer = serializationState!.Writer;

        Debug.Assert(writer != null, "writer was null");

        if (resource!.Attributes is IReadOnlyList<KeyValuePair<string, object?>> resourceAttributeList)
        {
            for (int i = 0; i < resourceAttributeList.Count; i++)
            {
                var resourceAttribute = resourceAttributeList[i];

                if (AttributeKeyStartWithExtensionPrefix(resourceAttribute.Key))
                {
                    serializationState.AddExtensionAttribute(resourceAttribute);
                    continue;
                }

                CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(resourceAttribute.Key, resourceAttribute.Value, writer!);
            }
        }
        else
        {
            foreach (KeyValuePair<string, object> resourceAttribute in resource.Attributes)
            {
                if (AttributeKeyStartWithExtensionPrefix(resourceAttribute.Key))
                {
                    serializationState.AddExtensionAttribute(resourceAttribute!);
                    continue;
                }

                CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(resourceAttribute.Key, resourceAttribute.Value, writer!);
            }
        }
    }

    protected abstract void SerializeItemToJson(Resource resource, T item, CommonSchemaJsonSerializationState serializationState);
}
