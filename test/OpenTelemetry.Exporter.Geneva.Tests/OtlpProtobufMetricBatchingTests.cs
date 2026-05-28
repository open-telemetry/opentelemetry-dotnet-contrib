// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable disable

using System.Buffers.Binary;
using System.Diagnostics.Metrics;
using Google.Protobuf;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Xunit;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter.Geneva.Tests;

/// <summary>
/// Tests for the OTLP-protobuf metric event batching behavior introduced to
/// reduce the number of UDS Socket.Send syscalls per export from O(metric
/// points) to O(scrape).
/// </summary>
public class OtlpProtobufMetricBatchingTests
{
    private const int BufferSize = 65360;

    [Fact]
    public void Flush_IsCalled_EvenWhenNoMetricPointsAreExported()
    {
        using var meter = new Meter(nameof(this.Flush_IsCalled_EvenWhenNoMetricPointsAreExported));
        var transport = new BatchingTestTransport();
        var serializer = new OtlpProtobufSerializer(
            transport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);

        serializer.SerializeAndSendMetrics(new byte[BufferSize], Resource.Empty, default);

        Assert.Equal(1, transport.FlushCount);
        Assert.Empty(transport.AppendedEvents);
        Assert.Empty(transport.StandaloneSends);
    }

    [Fact]
    public void ManyMetricPoints_AreAppendedAndFlushedExactlyOnce_NoOverflow()
    {
        var (resource, batch) = BuildCounterBatch(
            instrumentName: "longcounter",
            uniqueTagValues: 50);

        var transport = new BatchingTestTransport();
        var serializer = new OtlpProtobufSerializer(
            transport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);

        serializer.SerializeAndSendMetrics(new byte[BufferSize], resource, batch);

        // With 50 small data points that comfortably fit in a single 65360-byte
        // accumulation buffer, the transport must observe one append per point
        // and exactly one flush per export — no per-point standalone sends.
        Assert.Equal(50, transport.AppendedEvents.Count);
        Assert.Equal(1, transport.FlushCount);
        Assert.Empty(transport.StandaloneSends);
    }

    [Fact]
    public void ManyMetricPoints_OverflowingAccumulationBuffer_TriggerIntermediateFlushes()
    {
        // Force overflow by using a transport whose accumulation buffer is
        // small enough to hold only a handful of events at a time.
        const int smallCap = 512;
        var transport = new BatchingTestTransport(capacityBytes: smallCap);

        var (resource, batch) = BuildCounterBatch(
            instrumentName: "longcounter",
            uniqueTagValues: 200);

        var serializer = new OtlpProtobufSerializer(
            transport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);

        serializer.SerializeAndSendMetrics(new byte[BufferSize], resource, batch);

        Assert.Equal(200, transport.AppendedEvents.Count);

        // Each append-then-fail forces a Flush, and there is always one
        // terminal Flush from SerializeAndSendMetrics. So FlushCount must be
        // strictly between 1 and (number of appends + 1).
        Assert.InRange(transport.FlushCount, 2, 201);

        // No event was large enough to bypass batching.
        Assert.Empty(transport.StandaloneSends);
    }

    [Fact]
    public void SinglePointLargerThanAccumulationBuffer_FallsBackToStandaloneSend()
    {
        // Cap so small that no serialized point fits. Every event must fall
        // through to the standalone SendOtlpProtobufEvent path for back-compat.
        var transport = new BatchingTestTransport(capacityBytes: 16);

        var (resource, batch) = BuildCounterBatch(
            instrumentName: "longcounter",
            uniqueTagValues: 1);

        var serializer = new OtlpProtobufSerializer(
            transport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);

        serializer.SerializeAndSendMetrics(new byte[BufferSize], resource, batch);

        Assert.Empty(transport.AppendedEvents);
        Assert.NotEmpty(transport.StandaloneSends);

        // FlushCount can be 2 (one intermediate flush triggered by the failed
        // append, one terminal flush from SerializeAndSendMetrics).
        Assert.InRange(transport.FlushCount, 1, 2);
    }

    [Fact]
    public void BatchedFlushedBytes_AreByteForByteEqualToConcatenatedPerEventBytes()
    {
        // Build the metric snapshot once so that timestamps inside each
        // serialized metric point are identical for both serialization paths.
        var (resource, metrics) = SnapshotCounterMetrics(
            instrumentName: "longcounter",
            uniqueTagValues: 25);

        // Path A: production batching path — one flush concatenates all
        // serialized events into a single transport write.
        var batchingTransport = new BatchingTestTransport();
        var batchingSerializer = new OtlpProtobufSerializer(
            batchingTransport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);
        batchingSerializer.SerializeAndSendMetrics(
            new byte[BufferSize],
            resource,
            new Batch<Metric>(metrics, metrics.Length));

        // Path B: legacy semantics — TryAppend forwards immediately so each
        // event materializes as a standalone send (this is how the ETW and
        // user_events transports behave).
        var perEventTransport = new PerEventPassThroughTransport();
        var perEventSerializer = new OtlpProtobufSerializer(
            perEventTransport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);
        perEventSerializer.SerializeAndSendMetrics(
            new byte[BufferSize],
            resource,
            new Batch<Metric>(metrics, metrics.Length));

        Assert.Single(batchingTransport.FlushedBlobs);
        var batchedBlob = batchingTransport.FlushedBlobs[0];

        var concatenated = new List<byte>();
        foreach (var ev in perEventTransport.Events)
        {
            concatenated.AddRange(ev);
        }

        Assert.Equal(concatenated.ToArray(), batchedBlob);

        // Sanity-check that the bytes still parse as a sequence of length-
        // prefixed ExportMetricsServiceRequest payloads — i.e. the wire
        // contract the Geneva MDSD receiver relies on for demultiplexing.
        var offset = 0;
        var parsedEvents = 0;
        while (offset < batchedBlob.Length)
        {
            Assert.True(offset + 4 <= batchedBlob.Length, "truncated length prefix in batched blob");
            var len = (int)BinaryPrimitives.ReadUInt32LittleEndian(batchedBlob.AsSpan(offset, 4));
            offset += 4;
            Assert.True(offset + len <= batchedBlob.Length, "length prefix exceeds remaining bytes");
            var request = new OtlpCollector.ExportMetricsServiceRequest();
            request.MergeFrom(batchedBlob.AsSpan(offset, len).ToArray());
            Assert.Single(request.ResourceMetrics);
            offset += len;
            parsedEvents++;
        }

        Assert.Equal(perEventTransport.Events.Count, parsedEvents);
    }

    [Fact]
    public void FlushFailure_DoesNotLeakBufferedBytesIntoNextExport()
    {
        var transport = new ThrowOnFlushTransport();
        var serializer = new OtlpProtobufSerializer(
            transport,
            metricsAccount: null,
            metricsNamespace: null,
            prepopulatedMetricDimensions: null,
            prefixBufferWithUInt32LittleEndianLength: true);

        var (resource, batch) = BuildCounterBatch(
            instrumentName: "longcounter",
            uniqueTagValues: 3);

        // First export: terminal flush throws. The exporter must swallow it
        // and return Failure rather than propagating, so the next export can
        // proceed cleanly.
        var result1 = serializer.SerializeAndSendMetrics(new byte[BufferSize], resource, batch);
        Assert.Equal(ExportResult.Failure, result1);
        Assert.Equal(3, transport.AppendedBytesAtFlushAttempt[0]);

        // Second export must start with a clean accumulation — i.e. it
        // should observe its own 3 appends, not 6.
        var (_, batch2) = BuildCounterBatch(
            instrumentName: "longcounter",
            uniqueTagValues: 3);
        serializer.SerializeAndSendMetrics(new byte[BufferSize], resource, batch2);
        Assert.Equal(3, transport.AppendedBytesAtFlushAttempt[1]);
    }

    private static (Resource Resource, Batch<Metric> Batch) BuildCounterBatch(
        string instrumentName,
        int uniqueTagValues,
        string meterName = null)
    {
        var (resource, metrics) = SnapshotCounterMetrics(instrumentName, uniqueTagValues, meterName);
        return (resource, new Batch<Metric>(metrics, metrics.Length));
    }

    private static (Resource Resource, Metric[] Metrics) SnapshotCounterMetrics(
        string instrumentName,
        int uniqueTagValues,
        string meterName = null)
    {
        meterName ??= nameof(SnapshotCounterMetrics) + "_" + Guid.NewGuid().ToString("N");
        using var meter = new Meter(meterName);
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        var counter = meter.CreateCounter<long>(instrumentName);
        for (var i = 0; i < uniqueTagValues; i++)
        {
            counter.Add(1, new KeyValuePair<string, object>("bucket", i));
        }

        meterProvider.ForceFlush();

        return (meterProvider.GetResource(), exportedItems.ToArray());
    }

    private sealed class BatchingTestTransport : IMetricDataTransport
    {
        private readonly int capacity;
        private readonly List<byte> accumulator = new();

        public BatchingTestTransport(int capacityBytes = BufferSize)
        {
            this.capacity = capacityBytes;
        }

        public List<byte[]> AppendedEvents { get; } = new();

        public List<byte[]> FlushedBlobs { get; } = new();

        public List<byte[]> StandaloneSends { get; } = new();

        public int FlushCount { get; private set; }

        public void Send(MetricEventType eventType, byte[] body, int size)
            => throw new NotImplementedException();

        public void SendOtlpProtobufEvent(byte[] body, int size)
        {
            var copy = new byte[size];
            Buffer.BlockCopy(body, 0, copy, 0, size);
            this.StandaloneSends.Add(copy);
        }

        public bool TryAppendOtlpProtobufEvent(byte[] body, int size)
        {
            if (size > this.capacity - this.accumulator.Count)
            {
                return false;
            }

            var copy = new byte[size];
            Buffer.BlockCopy(body, 0, copy, 0, size);
            this.AppendedEvents.Add(copy);
            for (var i = 0; i < size; i++)
            {
                this.accumulator.Add(body[i]);
            }

            return true;
        }

        public void FlushOtlpProtobufEvents()
        {
            this.FlushCount++;
            if (this.accumulator.Count == 0)
            {
                return;
            }

            this.FlushedBlobs.Add(this.accumulator.ToArray());
            this.accumulator.Clear();
        }

        public void Dispose()
        {
        }
    }

    private sealed class PerEventPassThroughTransport : IMetricDataTransport
    {
        public List<byte[]> Events { get; } = new();

        public void Send(MetricEventType eventType, byte[] body, int size)
            => throw new NotImplementedException();

        public void SendOtlpProtobufEvent(byte[] body, int size)
        {
            var copy = new byte[size];
            Buffer.BlockCopy(body, 0, copy, 0, size);
            this.Events.Add(copy);
        }

        public bool TryAppendOtlpProtobufEvent(byte[] body, int size)
        {
            this.SendOtlpProtobufEvent(body, size);
            return true;
        }

        public void FlushOtlpProtobufEvents()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class ThrowOnFlushTransport : IMetricDataTransport
    {
        private int appendedThisExport;

        public List<int> AppendedBytesAtFlushAttempt { get; } = new();

        public void Send(MetricEventType eventType, byte[] body, int size)
            => throw new NotImplementedException();

        public void SendOtlpProtobufEvent(byte[] body, int size)
        {
        }

        public bool TryAppendOtlpProtobufEvent(byte[] body, int size)
        {
            this.appendedThisExport++;
            return true;
        }

        public void FlushOtlpProtobufEvents()
        {
            this.AppendedBytesAtFlushAttempt.Add(this.appendedThisExport);
            this.appendedThisExport = 0;
            throw new InvalidOperationException("simulated transport failure");
        }

        public void Dispose()
        {
        }
    }
}
