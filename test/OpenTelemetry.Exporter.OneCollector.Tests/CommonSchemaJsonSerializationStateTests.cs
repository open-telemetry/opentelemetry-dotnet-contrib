// <copyright file="CommonSchemaJsonSerializationStateTests.cs" company="OpenTelemetry Authors">
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

using System.Text;
using System.Text.Json;
using Xunit;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

public class CommonSchemaJsonSerializationStateTests
{
    [Fact]
    public void AddExtensionAttributeTest()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var state = new CommonSchemaJsonSerializationState("Test", writer);

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field1", 1));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field2", 2));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field3", 3));

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.food.field1", 4));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.food.field2", 5));

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field4", 6));

        Assert.Equal(2, state.ExtensionPropertyCount);
        Assert.Equal(6, state.ExtensionAttributeCount);

        writer.WriteStartObject();
        state.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: true);
        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(
            "{\"ext\":{\"something\":{\"field1\":1,\"field2\":2,\"field3\":3,\"field4\":6},\"food\":{\"field1\":4,\"field2\":5}}}",
            json);

        stream.SetLength(0);
        writer.Reset(stream);
        state.Reset("Test", writer);

        Assert.Equal(0, state.ExtensionPropertyCount);
        Assert.Equal(0, state.ExtensionAttributeCount);

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field1", 1));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.food.field1", 1));

        Assert.Equal(2, state.ExtensionPropertyCount);
        Assert.Equal(2, state.ExtensionAttributeCount);

        writer.WriteStartObject();
        state.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: true);
        writer.WriteEndObject();
        writer.Flush();

        json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(
            "{\"ext\":{\"something\":{\"field1\":1},\"food\":{\"field1\":1}}}",
            json);
    }

    [Fact]
    public void AddExtensionAttributeWithComplexTypeTest()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var state = new CommonSchemaJsonSerializationState("Test", writer);

        state.AddExtensionAttribute(new KeyValuePair<string, object?>(
            "ext.metadata",
            new Dictionary<string, object>
            {
                ["f"] = new Dictionary<string, object>
                {
                    ["Id"] = 1234,
                },
            }));

        Assert.Equal(1, state.ExtensionPropertyCount);
        Assert.Equal(1, state.ExtensionAttributeCount);

        writer.WriteStartObject();
        state.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: true);
        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(
            "{\"ext\":{\"metadata\":{\"f\":{\"Id\":1234}}}}",
            json);
    }

    [Fact]
    public void AddExtensionAttributeDuplicatesTest()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var state = new CommonSchemaJsonSerializationState("Test", writer);

        // Note: This test is just to verify de-duping is NOT currently supported.

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field1", 1));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.something.field1", 2));

        Assert.Equal(1, state.ExtensionPropertyCount);
        Assert.Equal(2, state.ExtensionAttributeCount);

        writer.WriteStartObject();
        state.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: true);
        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(
            "{\"ext\":{\"something\":{\"field1\":1,\"field1\":2}}}",
            json);
    }

    [Fact]
    public void AddExtensionAttributeMixedFieldAndPropertyTest()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var state = new CommonSchemaJsonSerializationState("Test", writer);

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext. foo .foo_field1", 1));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.foo", new List<KeyValuePair<string, object?>> { new KeyValuePair<string, object?>(" foo.field2 ", 2) }));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.foo .foo_field3 ", 3));

        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext. bar ", new Dictionary<string, object?>
        {
            ["   "] = 99, // Note: This will be dropped but the second entry will be added.
            ["bar_field1"] = 1,
        }));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext. bar .bar.field2", 2));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.bar.bar_field3", 3));

        // Note: These fields should all be ignored.
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.faz. ", 1));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.baz", new List<KeyValuePair<string, object?>> { new KeyValuePair<string, object?>(null!, 1) }));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.baz", new List<KeyValuePair<string, object?>> { new KeyValuePair<string, object?>(string.Empty, 2) }));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.baz", new List<KeyValuePair<string, object?>> { new KeyValuePair<string, object?>("    ", 3) }));
        state.AddExtensionAttribute(new KeyValuePair<string, object?>("ext.jazz", "Only IReadOnlyList and IEnumerable maps are supported"));

        Assert.Equal(4, state.ExtensionPropertyCount);
        Assert.Equal(6, state.ExtensionAttributeCount);

        writer.WriteStartObject();
        state.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: true);
        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal(
            "{\"ext\":{\"foo\":{\"foo_field1\":1,\"foo.field2\":2,\"foo_field3\":3},\"bar\":{\"bar_field1\":1,\"bar.field2\":2,\"bar_field3\":3}}}",
            json);
    }

    [Fact]
    public void AddExtensionAttributeKeyLimitTest()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var state = new CommonSchemaJsonSerializationState("Test", writer);

        for (int i = 0; i < CommonSchemaJsonSerializationState.MaxNumberOfExtensionKeys + 10; i++)
        {
            state.AddExtensionAttribute(new KeyValuePair<string, object?>($"ext.something{i}.field1", 1));
        }

        Assert.Equal(CommonSchemaJsonSerializationState.MaxNumberOfExtensionKeys, state.ExtensionPropertyCount);
        Assert.Equal(CommonSchemaJsonSerializationState.MaxNumberOfExtensionKeys, state.ExtensionAttributeCount);
    }

    [Fact]
    public void AddExtensionAttributeKeyValueLimitTest()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var state = new CommonSchemaJsonSerializationState("Test", writer);

        for (int i = 0; i < CommonSchemaJsonSerializationState.MaxNumberOfExtensionValuesPerKey + 10; i++)
        {
            state.AddExtensionAttribute(new KeyValuePair<string, object?>($"ext.something.field{i}", i));
        }

        Assert.Equal(1, state.ExtensionPropertyCount);
        Assert.Equal(CommonSchemaJsonSerializationState.MaxNumberOfExtensionValuesPerKey, state.ExtensionAttributeCount);
    }
}
