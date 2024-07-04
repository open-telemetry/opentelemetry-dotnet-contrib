// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Confluent.Kafka;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection("Kafka")]
public class TracingTests
{
    /*
        To run the integration tests, set the OTEL_KAFKAENDPOINT machine-level environment variable to a valid Kafka endpoint.

        To use Docker...
         1) Run: docker run -d --name kafka -p 9092:9092 confluentinc/confluent-local
         2) Set OTEL_KAFKAENDPOINT as: localhost:9092
     */

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task BasicProduceAsyncToTopicTest()
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        string topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using IProducer<string, string> producer = producerBuilder.Build();
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " publish");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("publish", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task BasicProduceAsyncToTopicPartitionTest()
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        string topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using IProducer<string, string> producer = producerBuilder.Build();
            await producer.ProduceAsync(new TopicPartition(topic, new Partition(0)), new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " publish");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("publish", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal(0, activity.GetTagValue("messaging.kafka.destination.partition"));
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public void BasicProduceSyncToTopicTest()
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        string topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using IProducer<string, string> producer = producerBuilder.Build();
            producer.Produce(topic, new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " publish");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("publish", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public void BasicProduceSyncToTopicPartitionTest()
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        string topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using IProducer<string, string> producer = producerBuilder.Build();
            producer.Produce(new TopicPartition(topic, new Partition(0)), new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " publish");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("publish", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal(0, activity.GetTagValue("messaging.kafka.destination.partition"));
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task BasicConsumeWithCancellationTokenTest()
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
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(activities)
                .SetSampler(sampler)
                .AddKafkaConsumerInstrumentation(consumerBuilder)
                .Build())
        {
            using IConsumer<string, string> consumer = consumerBuilder.Build();
            consumer.Subscribe(topic);
            while (true)
            {
                var consumeResult = consumer.Consume();
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
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " receive");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal(0, activity.GetTagValue("messaging.kafka.destination.partition"));
        Assert.Equal(0L, activity.GetTagValue("messaging.kafka.message.offset"));
        Assert.Equal("test-consumer-group", activity.GetTagValue("messaging.kafka.consumer.group"));
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task BasicConsumeWithTimeoutMsTest()
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
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(activities)
                .SetSampler(sampler)
                .AddKafkaConsumerInstrumentation(consumerBuilder)
                .Build())
        {
            using IConsumer<string, string> consumer = consumerBuilder.Build();
            consumer.Subscribe(topic);
            while (true)
            {
                var consumeResult = consumer.Consume(100);
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
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " receive");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal(0, activity.GetTagValue("messaging.kafka.destination.partition"));
        Assert.Equal(0L, activity.GetTagValue("messaging.kafka.message.offset"));
        Assert.Equal("test-consumer-group", activity.GetTagValue("messaging.kafka.consumer.group"));
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
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(activities)
                .SetSampler(sampler)
                .AddKafkaConsumerInstrumentation(consumerBuilder)
                .Build())
        {
            using IConsumer<string, string> consumer = consumerBuilder.Build();
            consumer.Subscribe(topic);
            while (true)
            {
                var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(100));
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
        }

        Assert.Contains(activities, activity => activity.DisplayName == topic + " receive");
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal(0, activity.GetTagValue("messaging.kafka.destination.partition"));
        Assert.Equal(0L, activity.GetTagValue("messaging.kafka.message.offset"));
        Assert.Equal("test-consumer-group", activity.GetTagValue("messaging.kafka.consumer.group"));
    }
}
