// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal static class KafkaProducerStatisticsParser
{
    public static KafkaProducerStatistics Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("type", out var type)
            || type.ValueKind != JsonValueKind.String
            || !string.Equals(type.GetString(), "producer", StringComparison.Ordinal))
        {
            throw new JsonException("Expected producer statistics.");
        }

        var totalBytes = GetNonNegativeInteger(root, "msg_size_max");
        var queuedBytes = GetNonNegativeInteger(root, "msg_size");
        long? availableBytes = totalBytes.HasValue && queuedBytes.HasValue
            ? Math.Max(0, totalBytes.Value - queuedBytes.Value)
            : null;
        long? requests = null;
        double queueSum = 0;
        double queueCount = 0;
        double throttleSum = 0;
        double throttleCount = 0;
        double? throttleMax = null;

        if (root.TryGetProperty("brokers", out var brokers) && brokers.ValueKind == JsonValueKind.Object)
        {
            foreach (var broker in brokers.EnumerateObject())
            {
                if (broker.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // Include bootstrap brokers: these are process totals, not node-labelled metrics.
                if (GetNonNegativeInteger(broker.Value, "waitresp_cnt") is { } count)
                {
                    requests = checked((requests ?? 0) + count);
                }

                ReadWindow(broker.Value, "int_latency", out var sum, out var samples, out _);
                queueSum += sum / 1000;
                queueCount += samples;

                // Unlike int_latency, librdkafka throttle windows are already in milliseconds.
                ReadWindow(broker.Value, "throttle", out sum, out samples, out var maximum);
                throttleSum += sum;
                throttleCount += samples;
                if (maximum.HasValue)
                {
                    throttleMax = Math.Max(throttleMax ?? 0, maximum.Value);
                }
            }
        }

        return new KafkaProducerStatistics(
            GetNonNegativeInteger(root, "ts"),
            ReadPartitionCounters(root),
            requests,
            totalBytes,
            availableBytes,
            queueSum,
            queueCount,
            throttleSum,
            throttleCount,
            throttleMax);
    }

    private static List<KafkaProducerStatistics.PartitionCounters> ReadPartitionCounters(JsonElement root)
    {
        var result = new List<KafkaProducerStatistics.PartitionCounters>();
        if (!root.TryGetProperty("topics", out var topics) || topics.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var topic in topics.EnumerateObject())
        {
            if (topic.Value.ValueKind != JsonValueKind.Object
                || !topic.Value.TryGetProperty("partitions", out var partitions)
                || partitions.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var partition in partitions.EnumerateObject())
            {
                // The unassigned partition (-1) is not included in native transmission totals.
                if (partition.Value.ValueKind != JsonValueKind.Object
                    || !int.TryParse(partition.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    continue;
                }

                var records = GetNonNegativeInteger(partition.Value, "txmsgs");
                var bytes = GetNonNegativeInteger(partition.Value, "txbytes");
                if (records.HasValue || bytes.HasValue)
                {
                    result.Add(new(topic.Name, id, records, bytes));
                }
            }
        }

        return result;
    }

    private static long? GetNonNegativeInteger(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
        && number >= 0
            ? number
            : null;

    private static void ReadWindow(JsonElement broker, string name, out double sum, out double count, out double? maximum)
    {
        sum = 0;
        count = 0;
        maximum = null;
        if (!broker.TryGetProperty(name, out var window)
            || window.ValueKind != JsonValueKind.Object
            || GetNonNegativeInteger(window, "cnt") is not > 0)
        {
            return;
        }

        maximum = GetNonNegativeInteger(window, "max");
        if (GetNonNegativeInteger(window, "sum") is { } windowSum)
        {
            sum = windowSum;
            count = GetNonNegativeInteger(window, "cnt")!.Value;
        }
    }
}
