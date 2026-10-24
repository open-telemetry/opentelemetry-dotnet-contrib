// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal static class KafkaProducerMetrics
{
    internal static readonly Counter<long> Records = ConfluentKafkaCommon.Meter.CreateCounter<long>(
        "kafka.producer.record_send_total", "{record}", "Number of records transmitted to Kafka brokers.");

    internal static readonly Counter<long> Bytes = ConfluentKafkaCommon.Meter.CreateCounter<long>(
        "kafka.producer.byte_total", "By", "Message bytes transmitted to Kafka brokers, including message and batch framing.");

    private static readonly ConcurrentDictionary<KafkaProducerMetricsRegistration, byte> Registrations = new();

    static KafkaProducerMetrics()
    {
        var meter = ConfluentKafkaCommon.Meter;
        meter.CreateObservableGauge("kafka.producer.requests_in_flight", () => ObserveSum(s => s.RequestsInFlight), "{request}", "Requests awaiting broker responses.");
        meter.CreateObservableGauge("kafka.producer.buffer_total_bytes", () => ObserveSum(s => s.BufferTotalBytes), "By", "Configured producer queue capacity.");
        meter.CreateObservableGauge("kafka.producer.buffer_available_bytes", () => ObserveSum(s => s.BufferAvailableBytes), "By", "Available producer queue capacity.");
        meter.CreateObservableGauge("kafka.producer.record_queue_time_avg", () => ObserveTiming(false, false), "ms", "Sample-weighted average internal producer queue latency in the latest statistics windows.");
        meter.CreateObservableGauge("kafka.producer.produce_throttle_time_avg", () => ObserveTiming(true, false), "ms", "Sample-weighted average broker throttle time in the latest statistics windows.");
        meter.CreateObservableGauge("kafka.producer.produce_throttle_time_max", () => ObserveTiming(true, true), "ms", "Maximum broker throttle time in the latest statistics windows.");
    }

    public static void Register(KafkaProducerMetricsRegistration registration) => Registrations.TryAdd(registration, 0);

    public static void Unregister(KafkaProducerMetricsRegistration registration) => Registrations.TryRemove(registration, out _);

    private static IEnumerable<Measurement<long>> ObserveSum(Func<KafkaProducerStatistics, long?> selector)
    {
        long? total = null;
        foreach (var registration in Registrations.Keys)
        {
            if (registration.Snapshot is { } snapshot && selector(snapshot) is { } value)
            {
                total = checked((total ?? 0) + value);
            }
        }

        if (total.HasValue)
        {
            yield return new Measurement<long>(total.Value);
        }
    }

    private static IEnumerable<Measurement<double>> ObserveTiming(bool throttle, bool maximum)
    {
        double sum = 0;
        double count = 0;
        double? max = null;
        foreach (var registration in Registrations.Keys)
        {
            if (registration.Snapshot is not { } snapshot)
            {
                continue;
            }

            sum += throttle ? snapshot.ThrottleTimeSum : snapshot.QueueTimeSum;
            count += throttle ? snapshot.ThrottleTimeCount : snapshot.QueueTimeCount;
            if (snapshot.ThrottleTimeMax is { } value)
            {
                max = Math.Max(max ?? 0, value);
            }
        }

        var result = maximum ? max : count > 0 ? sum / count : (double?)null;
        if (result.HasValue)
        {
            yield return new Measurement<double>(result.Value);
        }
    }
}
