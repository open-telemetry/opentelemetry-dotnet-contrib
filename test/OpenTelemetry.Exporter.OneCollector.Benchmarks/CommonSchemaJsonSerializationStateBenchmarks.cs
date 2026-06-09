// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Exporter.OneCollector.Benchmarks;

[MemoryDiagnoser]
public class CommonSchemaJsonSerializationStateBenchmarks
{
    private KeyValuePair<string, object?>[] attributes = [];
    private MemoryStream? stream;
    private Utf8JsonWriter? writer;
    private CommonSchemaJsonSerializationState? state;

    [Params(4, 8, 32)]
    public int ExtensionKeyCount { get; set; }

    [Params(2, 8)]
    public int ValuesPerExtensionKey { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.stream = new MemoryStream();
        this.writer = new Utf8JsonWriter(this.stream);
        this.state = new CommonSchemaJsonSerializationState("Benchmark", this.writer);

        this.attributes = new KeyValuePair<string, object?>[this.ExtensionKeyCount * this.ValuesPerExtensionKey];

        var index = 0;
        for (var keyIndex = 0; keyIndex < this.ExtensionKeyCount; keyIndex++)
        {
            for (var valueIndex = 0; valueIndex < this.ValuesPerExtensionKey; valueIndex++)
            {
                this.attributes[index++] = new KeyValuePair<string, object?>(
                    $"ext.group{keyIndex}.field{valueIndex}",
                    $"value-{keyIndex}-{valueIndex}");
            }
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.writer?.Dispose();
        this.stream?.Dispose();
    }

    [Benchmark]
    public int AddExtensionAttributes()
    {
        this.ResetState();

        foreach (var attribute in this.attributes)
        {
            this.state!.AddExtensionAttribute(attribute);
        }

        return this.state!.ExtensionAttributeCount;
    }

    [Benchmark]
    public int AddAndSerializeExtensionAttributes()
    {
        this.ResetState();

        foreach (var attribute in this.attributes)
        {
            this.state!.AddExtensionAttribute(attribute);
        }

        this.writer!.WriteStartObject();
        this.state!.SerializeExtensionPropertiesToJson(writeExtensionObjectEnvelope: true);
        this.writer!.WriteEndObject();
        this.writer!.Flush();

        return (int)this.stream!.Length;
    }

    private void ResetState()
    {
        this.stream!.SetLength(0);
        this.writer!.Reset(this.stream);
        this.state!.Reset("Benchmark", this.writer);
        this.state!.BeginItem();
    }
}
