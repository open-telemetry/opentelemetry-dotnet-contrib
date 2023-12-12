// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection("Kafka")]
public class MeteringTests
{
    private readonly ITestOutputHelper outputHelper;
    /*
        To run the integration tests, set the OTEL_KAFKAENDPOINT machine-level environment variable to a valid Kafka endpoint.

        To use Docker...
         1) Run: docker run -d --name kafka -p 9092:9092 confluentinc/confluent-local
         2) Set OTEL_KAFKAENDPOINT as: localhost:9092
     */

    public MeteringTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task BasicProduceAsyncToTopicTest()
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var metrics = new List<Metric>();
        string topic = $"otel-topic-{Guid.NewGuid()}";
        using (var meterBuilder = Sdk.CreateMeterProviderBuilder()
                   .AddInMemoryExporter(metrics)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            IProducer<string, string> producer = producerBuilder.Build();
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Value = "any_value",
            });

            // default ProducerConfig.StatisticsIntervalMs set by InstrumentedProducerBuilder is 1000
            // so we need to wait a bit longer than that to get the metrics
            await Task.Delay(1100);

            // poll to make sure the metrics are flushed
            producer.Poll(TimeSpan.FromSeconds(1));

            meterBuilder.ForceFlush();
        }

        var groups = from m in metrics
                     group m by m.Name into g
                     select new { MetricName = g.Key, Count = g.Count() };

        Assert.Equal(11, groups.Count());
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task BasicConsumeWithTimeoutTimespanTest()
    {
        string topic = await KafkaHelpers.ProduceTestMessageAsync();

        ConsumerConfig consumerConfig = new ConsumerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
            GroupId = "test-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = true,
        };
        InstrumentedConsumerBuilder<string, string> consumerBuilder = new(consumerConfig);

        var metrics = new List<Metric>();
        using (var meterBuilder = Sdk.CreateMeterProviderBuilder()
                .AddInMemoryExporter(metrics)
                .AddKafkaConsumerInstrumentation(consumerBuilder)
                .Build())
        {
            IConsumer<string, string> consumer = consumerBuilder.Build();
            consumer.Subscribe(topic);
            while (true)
            {
                var consumeResult = consumer.Consume();

                // default ConsumerConfig.StatisticsIntervalMs set by InstrumentedConsumerBuilder is 1000
                // so we need to wait a bit longer than that to get the metrics
                // we wait here as the topic contains only one message and the consumer StatisticsHandler is called
                // by librdkafka in the consumer loop
                await Task.Delay(1100);

                if (consumeResult == null)
                {
                    continue;
                }

                if (consumeResult.IsPartitionEOF)
                {
                    break;
                }
            }

            consumer.Close();

            meterBuilder.ForceFlush();
        }

        var groups = from m in metrics
                     group m by m.Name into g
                     select new { MetricName = g.Key, Count = g.Count() };

        Assert.Equal(11, groups.Count());
    }
}
