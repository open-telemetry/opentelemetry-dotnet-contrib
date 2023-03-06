// <copyright file="WriteDirectlyToTransportSink.cs" company="OpenTelemetry Authors">
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

#if NETFRAMEWORK || NETSTANDARD2_0
using System.Buffers;
#endif
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OneCollector;

internal sealed class WriteDirectlyToTransportSink<T> : ISink<T>, IDisposable
    where T : class
{
    private readonly string typeName;
    private readonly ISerializer<T> serializer;
    private readonly MemoryStream buffer;

    public WriteDirectlyToTransportSink(
        ISerializer<T> serializer,
        ITransport transport,
        int initialBufferCapacity = 8192)
    {
        Guard.ThrowIfNull(serializer);
        Guard.ThrowIfNull(transport);
        Guard.ThrowIfOutOfRange(initialBufferCapacity, min: 0);

        this.typeName = typeof(T).Name;
        this.serializer = serializer;
        this.Transport = transport;
        this.buffer = new(initialBufferCapacity);
    }

    public string Description => "WriteDirectlyToTransportSink";

    public ITransport Transport { get; }

    internal MemoryStream Buffer => this.buffer;

    public void Dispose()
    {
        this.TrySendRemainingData();

        (this.serializer as IDisposable)?.Dispose();
        (this.Transport as IDisposable)?.Dispose();
    }

    public int Write(Resource resource, in Batch<T> batch)
    {
        Span<byte> remainingData = default;

        var buffer = this.buffer;

        try
        {
            this.serializer.SerializeBatchOfItemsToStream(resource, in batch, buffer, (int)buffer.Length, out var serializationResult);

            var numberOfItemsSerialized = serializationResult.NumberOfItemsSerialized;

            if (numberOfItemsSerialized <= 0)
            {
                return 0;
            }

            OneCollectorExporterEventSource.Log.WriteSinkDataWrittenEventIfEnabled(this.typeName, numberOfItemsSerialized, this.Description);

            var numberOfItemsToSend = numberOfItemsSerialized;

            if (serializationResult.PayloadOverflowItemSizeInBytes.HasValue)
            {
                var hasUnderlyingBuffer = buffer.TryGetBuffer(out var underlyingBuffer);
                Debug.Assert(hasUnderlyingBuffer, "Could not access underlying buffer");

                var endPositionOfValidMessages = (int)(serializationResult.PayloadSizeInBytes - serializationResult.PayloadOverflowItemSizeInBytes);

                remainingData = underlyingBuffer.AsSpan().Slice(
                    endPositionOfValidMessages,
                    (int)serializationResult.PayloadOverflowItemSizeInBytes.Value);

                buffer.SetLength(endPositionOfValidMessages);

                numberOfItemsToSend--;
            }

            buffer.Position = 0;

            if (!this.Transport.Send(
                new TransportSendRequest
                {
                    ItemType = this.typeName,
                    ItemSerializationFormat = this.serializer.SerializationFormat,
                    ItemStream = buffer,
                    NumberOfItems = numberOfItemsToSend,
                }))
            {
                OneCollectorExporterEventSource.Log.DataDropped(this.typeName, numberOfItemsToSend);
            }

            return numberOfItemsSerialized;
        }
        finally
        {
            if (remainingData.Length > 0)
            {
                buffer.Position = 0;
#if NETFRAMEWORK || NETSTANDARD2_0
                var rentedBuffer = ArrayPool<byte>.Shared.Rent(remainingData.Length);
                try
                {
                    remainingData.CopyTo(rentedBuffer);
                    buffer.Write(rentedBuffer, 0, remainingData.Length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
#else
                buffer.Write(remainingData);
#endif
                buffer.SetLength(remainingData.Length);
            }
            else
            {
                buffer.SetLength(0);
            }
        }
    }

    private void TrySendRemainingData()
    {
        var buffer = this.buffer;
        if (buffer != null && buffer.Length > 0)
        {
            buffer.Position = 0;

            try
            {
                if (!this.Transport.Send(
                    new TransportSendRequest
                    {
                        ItemType = this.typeName,
                        ItemSerializationFormat = this.serializer.SerializationFormat,
                        ItemStream = buffer,
                        NumberOfItems = 1,
                    }))
                {
                    OneCollectorExporterEventSource.Log.DataDropped(this.typeName, 1);
                }
            }
            catch (Exception ex)
            {
                OneCollectorExporterEventSource.Log.DataDropped(this.typeName, 1);
                OneCollectorExporterEventSource.Log.WriteExportExceptionThrownEventIfEnabled(this.typeName, ex);
            }
            finally
            {
                buffer.SetLength(0);
            }
        }
    }
}
