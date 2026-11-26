// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class KafkaProducerStatistics
{
    public KafkaProducerStatistics(
        long? timestamp,
        IReadOnlyList<PartitionCounters> partitions,
        long? requestsInFlight,
        long? bufferTotalBytes,
        long? bufferAvailableBytes,
        double queueTimeSum,
        double queueTimeCount,
        double throttleTimeSum,
        double throttleTimeCount,
        double? throttleTimeMax)
    {
        this.Timestamp = timestamp;
        this.Partitions = partitions;
        this.RequestsInFlight = requestsInFlight;
        this.BufferTotalBytes = bufferTotalBytes;
        this.BufferAvailableBytes = bufferAvailableBytes;
        this.QueueTimeSum = queueTimeSum;
        this.QueueTimeCount = queueTimeCount;
        this.ThrottleTimeSum = throttleTimeSum;
        this.ThrottleTimeCount = throttleTimeCount;
        this.ThrottleTimeMax = throttleTimeMax;
    }

    public long? Timestamp { get; }

    public IReadOnlyList<PartitionCounters> Partitions { get; }

    public long? RequestsInFlight { get; }

    public long? BufferTotalBytes { get; }

    public long? BufferAvailableBytes { get; }

    public double QueueTimeSum { get; }

    public double QueueTimeCount { get; }

    public double ThrottleTimeSum { get; }

    public double ThrottleTimeCount { get; }

    public double? ThrottleTimeMax { get; }

    internal readonly record struct PartitionCounters(string Topic, int Partition, long? Records, long? Bytes)
    {
        public string Topic { get; } = Topic;

        public int Partition { get; } = Partition;

        public long? Records { get; } = Records;

        public long? Bytes { get; } = Bytes;
    }
}
