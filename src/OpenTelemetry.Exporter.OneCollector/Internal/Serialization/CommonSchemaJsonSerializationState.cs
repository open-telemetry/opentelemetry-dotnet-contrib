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

using System.Runtime.InteropServices;
using System.Text.Json;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class CommonSchemaJsonSerializationState
{
    private const int MaxNumberOfExtensionKeys = 64;
    private readonly Dictionary<string, int> keys = new(4, StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyValuePair<string, object?>> allValues = new(16);
    private int nextKeysToAllValuesLookupIndex;
    private KeyValueLookup[] keysToAllValuesLookup = new KeyValueLookup[4];

    public CommonSchemaJsonSerializationState(Utf8JsonWriter writer)
    {
        this.Writer = writer;
    }

    public Utf8JsonWriter Writer { get; private set; }

    public int ExtensionPropertyCount => this.keys.Count;

    public int ExtensionAttributeCount => this.allValues.Count;

    public void AddExtensionAttribute(KeyValuePair<string, object?> attribute)
    {
        if (!ExtensionFieldInformationManager.TryResolveExtensionFieldInformation(attribute.Key, out (string ExtensionName, string FieldName) fieldInformation))
        {
            // TODO: Log dropped invalid attribute
            return;
        }

#if NET6_0_OR_GREATER
        ref var lookupIndex = ref CollectionsMarshal.GetValueRefOrAddDefault(this.keys, fieldInformation.ExtensionName, out var existed);
        if (!existed)
        {
            this.AssignNewExtensionToLookupIndex(ref lookupIndex);
        }
#else
        if (!this.keys.TryGetValue(fieldInformation.ExtensionName, out int lookupIndex))
        {
            this.AssignNewExtensionToLookupIndex(ref lookupIndex);
            this.keys[fieldInformation.ExtensionName] = lookupIndex;
        }
#endif

        if (lookupIndex == -1)
        {
            // TODO: Log dropped attribute
            return;
        }

        ref KeyValueLookup keyLookup = ref this.keysToAllValuesLookup[lookupIndex];

        if (keyLookup.Count >= KeyValueLookup.MaxNumberOfValues)
        {
            // TODO: Log dropped attribute
            return;
        }

        int index = this.allValues.Count;
        this.allValues.Add(new KeyValuePair<string, object?>(fieldInformation.FieldName, attribute.Value));

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
            writer.WriteStartObject(extensionPropertyKey.Key);

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

                    CommonSchemaJsonSerializationHelper.SerializeKeyValueToJson(attribute.Key, attribute.Value, writer);
                }
            }

            writer.WriteEndObject();
        }

        if (writeExtensionObjectEnvelope)
        {
            writer.WriteEndObject();
        }
    }

    public void Reset(Utf8JsonWriter writer)
    {
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
        public const int MaxNumberOfValues = 16;

        public int Count;
        public fixed int ValueIndicies[MaxNumberOfValues];
    }
}
