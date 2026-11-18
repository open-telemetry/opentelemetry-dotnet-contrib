// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class KafkaProducerStatistics
{
    public KafkaProducerStatistics(
        long? timestamp,
        long? records,
        long? bytes,
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
        this.Records = records;
        this.Bytes = bytes;
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

    public long? Records { get; }

    public long? Bytes { get; }

    public long? RequestsInFlight { get; }

    public long? BufferTotalBytes { get; }

    public long? BufferAvailableBytes { get; }

    public double QueueTimeSum { get; }

    public double QueueTimeCount { get; }

    public double ThrottleTimeSum { get; }

    public double ThrottleTimeCount { get; }

    public double? ThrottleTimeMax { get; }
}
