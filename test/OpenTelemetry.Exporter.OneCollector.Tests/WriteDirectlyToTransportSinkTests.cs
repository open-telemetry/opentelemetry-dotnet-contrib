// <copyright file="WriteDirectlyToTransportSinkTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

// Suppressed because the underscores help with readability. That and artistic
// license!
#pragma warning disable CA1707 // Identifiers should not contain underscores

public class WriteDirectlyToTransportSinkTests
{
    [Fact]
    public void FullDataWrittenToTransportTest()
    {
        var transport = new TestTransport();

        using var sink = new WriteDirectlyToTransportSink<string>(
            new TestSerializer(),
            transport);

        var items = new string[]
        {
            "item1",
            "item2",
            "item3",
        };

        var numberOfRecordsWritten = sink.Write(Resource.Empty, new(items, items.Length));

        Assert.Equal(3, numberOfRecordsWritten);

        var data = transport.ExportedData[0];

        Assert.NotNull(data);

        Assert.Equal("\"item1\"\n\"item2\"\n\"item3\"\n", Encoding.ASCII.GetString(data));

        Assert.Equal(0, sink.Buffer.Length);
    }

    [Fact]
    public void PartialDataWrittenToTransport_MaxItemsPerPayload_Test()
    {
        var transport = new TestTransport();

        using var sink = new WriteDirectlyToTransportSink<string>(
            new TestSerializer(maxNumberOfItemsPerPayload: 2),
            transport);

        var items = new string[]
        {
            "item1",
            "item2",
            "item3",
        };

        var numberOfRecordsWritten = sink.Write(Resource.Empty, new(items, items.Length));

        Assert.Equal(2, numberOfRecordsWritten);

        var data = transport.ExportedData[0];

        Assert.NotNull(data);

        Assert.Equal("\"item1\"\n\"item2\"\n", Encoding.ASCII.GetString(data));

        Assert.Equal(0, sink.Buffer.Length);
    }

    [Fact]
    public void PartialDataWrittenToTransport_MaxPayloadSize_Test()
    {
        var expectedPayloadSizeInBytes = "\"item1\"\n\"item2\"\n".Length;

        var transport = new TestTransport();

        var sink = new WriteDirectlyToTransportSink<string>(
            new TestSerializer(maxPayloadSizeInBytes: expectedPayloadSizeInBytes + 1),
            transport);

        var items = new string[]
        {
            "item1",
            "item2",
            "item3",
        };

        var numberOfRecordsWritten = sink.Write(Resource.Empty, new(items, items.Length));

        Assert.Equal(3, numberOfRecordsWritten);

        var data = transport.ExportedData[0];

        Assert.NotNull(data);
        Assert.Equal(expectedPayloadSizeInBytes, data.Length);
        Assert.Equal("\"item1\"\n\"item2\"\n", Encoding.ASCII.GetString(data));

        Assert.NotEqual(0, sink.Buffer.Length);

        items = new string[]
        {
            "item4",
            "item5",
        };

        numberOfRecordsWritten = sink.Write(Resource.Empty, new(items, items.Length));

        Assert.Equal(2, numberOfRecordsWritten);

        data = transport.ExportedData[1];

        Assert.NotNull(data);
        Assert.Equal(expectedPayloadSizeInBytes, data.Length);
        Assert.Equal("\"item3\"\n\"item4\"\n", Encoding.ASCII.GetString(data));

        Assert.NotEqual(0, sink.Buffer.Length);

        sink.Dispose();

        Assert.Equal(0, sink.Buffer.Length);

        data = transport.ExportedData[2];

        Assert.NotNull(data);
        Assert.Equal("\"item5\"\n", Encoding.ASCII.GetString(data));
    }

    private sealed class TestSerializer : CommonSchemaJsonSerializer<string>
    {
        public TestSerializer(
            int maxPayloadSizeInBytes = int.MaxValue,
            int maxNumberOfItemsPerPayload = int.MaxValue)
            : base("tenant-token", maxPayloadSizeInBytes, maxNumberOfItemsPerPayload)
        {
        }

        public override string Description => nameof(TestSerializer);

        protected override void SerializeItemToJson(Resource resource, string item, Utf8JsonWriter writer)
        {
            writer.WriteStringValue(item);
        }
    }

    /*
    private sealed class TestSerializer : ISerializer<string>
    {
        private readonly int maxPayloadSizeInBytes;
        private readonly int maxNumberOfItems;

        public TestSerializer(int maxNumberOfItems = int.MaxValue)
        {
            this.maxNumberOfItems = maxNumberOfItems;
        }

        public string Description => nameof(TestSerializer);

        public void SerializeBatchOfItemsToStream(
            Resource resource,
            in Batch<string> batch,
            Stream stream,
            out BatchSerializationResult serializationResult)
        {
            var numberOfItemsSerialized = 0;

            stream.WriteByte((byte)'[');

            foreach (var item in batch)
            {
                if (numberOfItemsSerialized > 0)
                {
                    stream.WriteByte((byte)',');
                }

                stream.WriteByte((byte)'"');

                byte[] data = Encoding.ASCII.GetBytes(item);
                stream.Write(data, 0, data.Length);

                stream.WriteByte((byte)'"');

                if (numberOfItemsSerialized++ >= this.maxNumberOfItems)
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

            stream.WriteByte((byte)']');

            serializationResult = new BatchSerializationResult
            {
                NumberOfItemsSerialized = numberOfItemsSerialized,
                PayloadSizeInBytes = stream.Length,
            };
        }
    }
    */

    private sealed class TestTransport : ITransport
    {
        public string Description => nameof(TestTransport);

        public List<byte[]> ExportedData { get; } = new();

        public bool Send(in TransportSendRequest sendRequest)
        {
            var stream = new MemoryStream();

            sendRequest.ItemStream.CopyTo(stream);

            this.ExportedData.Add(stream.ToArray());

            return true;
        }
    }
}
