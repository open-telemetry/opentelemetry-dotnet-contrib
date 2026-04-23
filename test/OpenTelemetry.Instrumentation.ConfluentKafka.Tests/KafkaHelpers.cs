// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

internal static class KafkaHelpers
{
    public static async Task<string> ProduceTestMessageAsync(string connectionString)
    {
        var topic = $"otel-topic-{Guid.NewGuid()}";
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = connectionString,
        };
        ProducerBuilder<string, string> producerBuilder = new(producerConfig);
        using var producer = producerBuilder.Build();
        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Value = "any_value",
        });
        return topic;
    }
}
