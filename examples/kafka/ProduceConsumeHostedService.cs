// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;

namespace Examples.ConfluentKafka;

public class ProduceConsumeHostedService(
    InstrumentedProducerBuilder<string, string> instrumentedProducerBuilder,
    InstrumentedConsumerBuilder<string, string> instrumentedConsumerBuilder)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IProducer<string, string> producer = instrumentedProducerBuilder.Build();
        IConsumer<string, string> consumer = instrumentedConsumerBuilder.Build();

        for (int j = 0; j < 100; j++)
        {
            await producer.ProduceAsync(
                Constants.Topic,
                new Message<string, string> { Key = "any_key", Value = $"any_value_{j}" },
                stoppingToken);
        }

        for (int j = 0; j < 100; j++)
        {
            producer.Produce(
                Constants.Topic,
                new Message<string, string> { Key = "any_key", Value = $"any_value_{j}" });
        }

        producer.Flush(stoppingToken);

        consumer.Subscribe(Constants.Topic);
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string> consumeResult = consumer.Consume(stoppingToken);
            if (consumeResult == null)
            {
                continue;
            }

            if (consumeResult.IsPartitionEOF)
            {
                break;
            }

            Console.WriteLine($"Consumer {consumer.Name} received message: {consumeResult.Message.Value}");
        }
    }
}
