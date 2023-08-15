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

#if NET6_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using System.Diagnostics;
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

    public CommonSchemaJsonSerializationState(string itemType, Utf8JsonWriter writer, OneCollectorExporterJsonSerializationOptions options)
    {
        this.itemType = itemType;
        this.Writer = writer;
        this.Options = options;
    }

    public Utf8JsonWriter Writer { get; private set; }

    public OneCollectorExporterJsonSerializationOptions Options { get; private set; }

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

        var keyCount = keyLookup.Count;
        if (keyCount > 0)
        {
            if (keyCount >= MaxNumberOfExtensionValuesPerKey)
            {
                OneCollectorExporterEventSource.Log.AttributeDropped(this.itemType, attribute.Key, "Extension value limit reached");
                return;
            }

            unsafe
            {
#if NET6_0_OR_GREATER
                ref var firstDefinition = ref CollectionsMarshal.AsSpan(this.allValues)[keyLookup.ValueIndicies[0]];
#else
                var firstDefinition = this.allValues[keyLookup.ValueIndicies[0]];
#endif

                if (fieldInformation.FieldName == null || firstDefinition.Key.FieldName == null)
                {
                    OneCollectorExporterEventSource.Log.AttributeDropped(this.itemType, attribute.Key, "Extension is already defined");
                    return;
                }
            }
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
        var options = this.Options;

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
            var wroteProperty = false;

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

                    if (fieldInformation.FieldName == null)
                    {
                        Debug.Assert(!wroteProperty, "wroteProperty");
                        Debug.Assert(!wroteStartObject, "wroteStartObject");

                        writer.WritePropertyName(fieldInformation.EncodedExtensionName);
                        CommonSchemaJsonSerializationHelper.SerializeValueToJson(attribute.Value, writer, options);
                        wroteProperty = true;
                    }
                    else
                    {
                        Debug.Assert(!wroteProperty, "wroteProperty");

                        if (!wroteStartObject)
                        {
                            writer.WriteStartObject(fieldInformation.EncodedExtensionName);
                            wroteStartObject = true;
                        }

                        writer.WritePropertyName(fieldInformation.EncodedFieldName);
                        CommonSchemaJsonSerializationHelper.SerializeValueToJson(attribute.Value, writer, options);
                    }
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

    public void Reset(string itemType, Utf8JsonWriter writer, OneCollectorExporterJsonSerializationOptions options)
    {
        this.itemType = itemType;
        this.Writer = writer;
        this.Options = options;

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
