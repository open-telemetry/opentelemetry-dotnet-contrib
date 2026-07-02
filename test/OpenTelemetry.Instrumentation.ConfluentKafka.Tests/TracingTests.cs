// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Confluent.Kafka;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection(KafkaCollection.Name)]
[Trait("CategoryName", "KafkaIntegrationTests")]
public class TracingTests(KafkaFixture fixture)
{
    private readonly string connectionString = fixture.TypedContainer.GetConnectionString();

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicProduceAsyncToTopicTest()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = this.connectionString,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        var topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using var producer = producerBuilder.Build();
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == "send " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal(ActivityKind.Producer, activity.Kind);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicProduceAsyncToTopicPartitionTest()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = this.connectionString,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        var topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using var producer = producerBuilder.Build();
            await producer.ProduceAsync(new TopicPartition(topic, new Partition(0)), new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == "send " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal("0", activity.GetTagValue("messaging.destination.partition.id"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void BasicProduceSyncToTopicTest()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = this.connectionString,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        var topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using var producer = producerBuilder.Build();
            producer.Produce(topic, new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == "send " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public void BasicProduceSyncToTopicPartitionTest()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = this.connectionString,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        var topic = $"otel-topic-{Guid.NewGuid()}";
        using (Sdk.CreateTracerProviderBuilder()
                   .AddInMemoryExporter(activities)
                   .SetSampler(sampler)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            using var producer = producerBuilder.Build();
            producer.Produce(new TopicPartition(topic, new Partition(0)), new Message<string, string>
            {
                Value = "any_value",
            });
        }

        Assert.Contains(activities, activity => activity.DisplayName == "send " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("send", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal("0", activity.GetTagValue("messaging.destination.partition.id"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicConsumeWithCancellationTokenTest()
    {
        var topic = await KafkaHelpers.ProduceTestMessageAsync(this.connectionString);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = this.connectionString,
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
            using var consumer = consumerBuilder.Build();
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

        Assert.Contains(activities, activity => activity.DisplayName == "poll " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal("0", activity.GetTagValue("messaging.destination.partition.id"));
        Assert.Equal(0L, activity.GetTagValue("messaging.kafka.offset"));
        Assert.Equal("test-consumer-group", activity.GetTagValue("messaging.consumer.group.name"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicConsumeWithTimeoutMsTest()
    {
        var topic = await KafkaHelpers.ProduceTestMessageAsync(this.connectionString);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = this.connectionString,
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
            using var consumer = consumerBuilder.Build();
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

        Assert.Contains(activities, activity => activity.DisplayName == "poll " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal("0", activity.GetTagValue("messaging.destination.partition.id"));
        Assert.Equal(0L, activity.GetTagValue("messaging.kafka.offset"));
        Assert.Equal("test-consumer-group", activity.GetTagValue("messaging.consumer.group.name"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicConsumeWithTimeoutTimespanTest()
    {
        var topic = await KafkaHelpers.ProduceTestMessageAsync(this.connectionString);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = this.connectionString,
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
            using var consumer = consumerBuilder.Build();
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

        Assert.Contains(activities, activity => activity.DisplayName == "poll " + topic);
        var activity = Assert.Single(activities);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("poll", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, activity.GetTagValue("messaging.destination.name"));
        Assert.Equal("0", activity.GetTagValue("messaging.destination.partition.id"));
        Assert.Equal(0L, activity.GetTagValue("messaging.kafka.offset"));
        Assert.Equal("test-consumer-group", activity.GetTagValue("messaging.consumer.group.name"));
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ConsumeAndProcessMessageTest()
    {
        var topic = await KafkaHelpers.ProduceTestMessageAsync(this.connectionString);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = this.connectionString,
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
            using var consumer = consumerBuilder.Build();
            consumer.Subscribe(topic);
            while (true)
            {
                var consumeResult = await consumer.ConsumeAndProcessMessageAsync(NoOpAsync);
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

        var processActivity = Assert.Single(activities, activity => activity.DisplayName == "process " + topic);

        Assert.Equal(ActivityKind.Consumer, processActivity.Kind);
        Assert.Equal("kafka", processActivity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("process", processActivity.GetTagValue(SemanticConventions.AttributeMessagingOperationName));
        Assert.Equal("process", processActivity.GetTagValue(SemanticConventions.AttributeMessagingOperationType));
        Assert.Equal(topic, processActivity.GetTagValue("messaging.destination.name"));
        Assert.Equal("0", processActivity.GetTagValue("messaging.destination.partition.id"));
        Assert.Equal(0L, processActivity.GetTagValue("messaging.kafka.offset"));
        Assert.Equal("test-consumer-group", processActivity.GetTagValue("messaging.consumer.group.name"));

        static ValueTask NoOpAsync(
            ConsumeResult<string, string> consumeResult,
            Activity? activity,
            CancellationToken cancellationToken = default)
        {
            return default;
        }
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ConsumeAndProcessMessageShouldPropagateException()
    {
        var topic = await KafkaHelpers.ProduceTestMessageAsync(this.connectionString);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = this.connectionString,
            GroupId = "test-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };
        InstrumentedConsumerBuilder<string, string> consumerBuilder = new(consumerConfig);

        using var consumer = consumerBuilder.Build();
        consumer.Subscribe(topic);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await consumer.ConsumeAndProcessMessageAsync(ExceptionOpAsync));

        static ValueTask ExceptionOpAsync(
            ConsumeResult<string, string> consumeResult,
            Activity? activity,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException();
        }
    }
}
