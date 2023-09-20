// <copyright file="CommonSchemaJsonSerializationState.cs" company="OpenTelemetry Authors">
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
#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using System.Text.Json;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class CommonSchemaJsonSerializationState
{
    public const int MaxNumberOfExtensionKeys = 64;
    public const int MaxNumberOfExtensionValuesPerKey = 16;
    private readonly Dictionary<string, int> keys = new(4, StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyValuePair<ExtensionFieldInformation, object?>> allValues = new(16);
    private string itemType;
    private int nextKeysToAllValuesLookupIndex;
    private KeyValueLookup[] keysToAllValuesLookup = new KeyValueLookup[4];

    public CommonSchemaJsonSerializationState(string itemType, Utf8JsonWriter writer)
    {
        this.itemType = itemType;
        this.Writer = writer;
    }

    public Utf8JsonWriter Writer { get; private set; }

    public int ExtensionPropertyCount => this.nextKeysToAllValuesLookupIndex;

    public int ExtensionAttributeCount => this.allValues.Count;

    public void AddExtensionAttribute(KeyValuePair<string, object?> attribute)
    {
        if (!ExtensionFieldInformationManager.SharedCache.TryResolveExtensionFieldInformation(
            attribute.Key,
            out var fieldInformation))
        {
            OneCollectorExporterEventSource.Log.AttributeDropped(this.itemType, attribute.Key, "Invalid extension field name");
            return;
        }

        Debug.Assert(fieldInformation?.ExtensionName != null, "fieldInformation.ExtensionName was null");
        Debug.Assert(fieldInformation?.EncodedExtensionName.EncodedUtf8Bytes.Length > 0, "fieldInformation.EncodedExtensionName was empty");
        Debug.Assert(fieldInformation?.FieldName != null, "fieldInformation.FieldName was null");
        Debug.Assert(fieldInformation?.EncodedFieldName.EncodedUtf8Bytes.Length > 0, "fieldInformation.EncodedFieldName was empty");

#if NET6_0_OR_GREATER
        ref var lookupIndex = ref CollectionsMarshal.GetValueRefOrAddDefault(this.keys, fieldInformation.ExtensionName, out var existed);
        if (!existed)
        {
            this.AssignNewExtensionToLookupIndex(ref lookupIndex);
        }
#else
        if (!this.keys.TryGetValue(fieldInformation!.ExtensionName!, out int lookupIndex))
        {
            this.AssignNewExtensionToLookupIndex(ref lookupIndex);
            this.keys[fieldInformation.ExtensionName!] = lookupIndex;
        }
#endif

        if (lookupIndex == -1)
        {
            OneCollectorExporterEventSource.Log.AttributeDropped(this.itemType, attribute.Key, "Extension limit reached");
            return;
        }

        ref KeyValueLookup keyLookup = ref this.keysToAllValuesLookup[lookupIndex];

        if (keyLookup.Count >= MaxNumberOfExtensionValuesPerKey)
        {
            OneCollectorExporterEventSource.Log.AttributeDropped(this.itemType, attribute.Key, "Extension value limit reached");
            return;
        }

        int index = this.allValues.Count;
        this.allValues.Add(new KeyValuePair<ExtensionFieldInformation, object?>(fieldInformation, attribute.Value));

        unsafe
        {
            keyLookup.ValueIndicies[keyLookup.Count++] = index;
        }
    }

    public void SerializeExtensionPropertiesToJson(bool writeExtensionObjectEnvelope)
    {
        var writer = this.Writer;

        if (writeExtensionObjectEnvelope)
        {
            writer.WriteStartObject(CommonSchemaJsonSerializationHelper.ExtensionsProperty);
        }

#if NET6_0_OR_GREATER
        var allValues = CollectionsMarshal.AsSpan(this.allValues);
#else
        var allValues = this.allValues;
#endif

        foreach (var extensionPropertyKey in this.keys)
        {
            var wroteStartObject = false;

            ref KeyValueLookup keyLookup = ref this.keysToAllValuesLookup[extensionPropertyKey.Value];

            for (int i = 0; i < keyLookup.Count; i++)
            {
                unsafe
                {
#if NET6_0_OR_GREATER
                    ref var attribute = ref allValues[keyLookup.ValueIndicies[i]];
#else
                    var attribute = allValues[keyLookup.ValueIndicies[i]];
#endif
                    var fieldInformation = attribute.Key;

                    if (!wroteStartObject)
                    {
                        writer.WriteStartObject(fieldInformation.EncodedExtensionName);
                        wroteStartObject = true;
                    }

                    writer.WritePropertyName(fieldInformation.EncodedFieldName);
                    CommonSchemaJsonSerializationHelper.SerializeValueToJson(attribute.Value, writer);
                }
            }

            if (wroteStartObject)
            {
                writer.WriteEndObject();
            }
        }

        if (writeExtensionObjectEnvelope)
        {
            writer.WriteEndObject();
        }
    }

    public void Reset(string itemType, Utf8JsonWriter writer)
    {
        this.itemType = itemType;
        this.Writer = writer;

        for (int i = 0; i < this.nextKeysToAllValuesLookupIndex; i++)
        {
            ref var lookup = ref this.keysToAllValuesLookup[i];
            lookup.Count = 0;
        }

        this.nextKeysToAllValuesLookupIndex = 0;

        this.keys.Clear();

        this.allValues.Clear();
    }

    private void AssignNewExtensionToLookupIndex(ref int lookupIndex)
    {
        lookupIndex = this.nextKeysToAllValuesLookupIndex;

        if (lookupIndex >= this.keysToAllValuesLookup.Length)
        {
            if (this.keysToAllValuesLookup.Length >= MaxNumberOfExtensionKeys)
            {
                lookupIndex = -1;
                return;
            }

            var newKeysToAllValuesLookup = new KeyValueLookup[this.keysToAllValuesLookup.Length * 2];
            this.keysToAllValuesLookup.CopyTo(newKeysToAllValuesLookup, 0);
            this.keysToAllValuesLookup = newKeysToAllValuesLookup;
        }

        this.nextKeysToAllValuesLookupIndex++;
    }

    private unsafe struct KeyValueLookup
    {
        public int Count;
        public fixed int ValueIndicies[MaxNumberOfExtensionValuesPerKey];
    }
}
