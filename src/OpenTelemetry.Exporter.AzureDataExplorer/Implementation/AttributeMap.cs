// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace OpenTelemetry.Exporter.AzureDataExplorer;

/// <summary>
/// Reusable, insertion-ordered attribute map where later values replace earlier
/// values with the same key, mirroring Go's <c>maps.Copy</c> semantics used by
/// the Collector exporter. Not thread-safe.
/// </summary>
internal sealed class AttributeMap
{
    private readonly List<string> keys = [];
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

    public int Count => this.keys.Count;

    public void Clear()
    {
        this.keys.Clear();
        this.values.Clear();
    }

    public void Set(string key, object? value)
    {
        if (!this.values.ContainsKey(key))
        {
            this.keys.Add(key);
        }

        this.values[key] = value;
    }

    public void SetAll(IEnumerable<KeyValuePair<string, object?>>? attributes)
    {
        if (attributes == null)
        {
            return;
        }

        foreach (var attribute in attributes)
        {
            this.Set(attribute.Key, attribute.Value);
        }
    }

    public void SetScope(string? name, string? version)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            this.Set(AdxJson.ScopeNameKey, name);
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            this.Set(AdxJson.ScopeVersionKey, version);
        }
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var key in this.keys)
        {
            writer.WritePropertyName(key);
            AdxJson.WriteValue(writer, this.values[key]);
        }

        writer.WriteEndObject();
    }
}
