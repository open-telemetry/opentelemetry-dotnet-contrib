// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection(KafkaCollection.Name)]
[Trait("CategoryName", "KafkaIntegrationTests")]
public class MeteringTests(KafkaFixture fixture)
{
    private readonly string connectionString = fixture.TypedContainer.GetConnectionString();

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicProduceToTopicTest()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = this.connectionString,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var metrics = new List<Metric>();
        var topic = $"otel-topic-{Guid.NewGuid()}";
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                   .AddInMemoryExporter(metrics)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            var producer = producerBuilder.Build();
            producer.Produce(topic, new Message<string, string>
            {
                Value = "any_value",
            });

            await producer.FlushAsync();

            meterProvider.EnsureMetricsAreFlushed();
        }

        var groups = metrics.GroupBy(m => m.Name).ToArray();

        Assert.Equal(2, groups.Length);
        Assert.Contains(SemanticConventions.MetricMessagingClientSentMessages, groups.Select(g => g.Key));
        Assert.Contains(SemanticConventions.MetricMessagingClientOperationDuration, groups.Select(g => g.Key));
        AssertOperation(metrics, SemanticConventions.MetricMessagingClientOperationDuration, expectedOperationName: "send", expectedOperationType: "send");
    }

    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task BasicProduceAsyncToTopicTest()
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = this.connectionString,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        var metrics = new List<Metric>();
        var topic = $"otel-topic-{Guid.NewGuid()}";
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                   .AddInMemoryExporter(metrics)
                   .AddKafkaProducerInstrumentation(producerBuilder)
                   .Build())
        {
            var producer = producerBuilder.Build();
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Value = "any_value",
            });

            await producer.FlushAsync();

            meterProvider.EnsureMetricsAreFlushed();
        }

        var groups = metrics.GroupBy(m => m.Name).ToArray();

        Assert.Equal(2, groups.Length);
        Assert.Contains(SemanticConventions.MetricMessagingClientSentMessages, groups.Select(g => g.Key));
        Assert.Contains(SemanticConventions.MetricMessagingClientOperationDuration, groups.Select(g => g.Key));
        AssertOperation(metrics, SemanticConventions.MetricMessagingClientOperationDuration, expectedOperationName: "send", expectedOperationType: "send");
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

        var metrics = new List<Metric>();
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddInMemoryExporter(metrics)
                .AddKafkaConsumerInstrumentation(consumerBuilder)
                .Build())
        {
            using (var consumer = consumerBuilder.Build())
            {
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

            meterProvider.EnsureMetricsAreFlushed();
        }

        var groups = metrics.GroupBy(m => m.Name).ToArray();

        Assert.Equal(2, groups.Length);
        Assert.Contains(SemanticConventions.MetricMessagingClientConsumedMessages, groups.Select(g => g.Key));
        Assert.Contains(SemanticConventions.MetricMessagingClientOperationDuration, groups.Select(g => g.Key));
        AssertOperation(metrics, SemanticConventions.MetricMessagingClientOperationDuration, expectedOperationName: "poll", expectedOperationType: "receive");
    }

    private static void AssertOperation(
        IEnumerable<Metric> metrics,
        string metricName,
        string expectedOperationName,
        string expectedOperationType)
    {
        // The in-memory exporter may emit the same instrument more than once (e.g. on flush and on
        // dispose), so inspect every matching metric rather than expecting a single instance.
        var matching = metrics.Where(m => m.Name == metricName).ToList();
        Assert.NotEmpty(matching);
        Assert.All(matching, m => Assert.StartsWith("https://opentelemetry.io/schemas/", m.MeterSchemaUrl, StringComparison.Ordinal));

        string? operationName = null;
        string? operationType = null;
        string? system = null;

        foreach (var metric in matching)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                foreach (var tag in metricPoint.Tags)
                {
                    switch (tag.Key)
                    {
                        case SemanticConventions.AttributeMessagingOperationName:
                            operationName = tag.Value?.ToString();
                            break;

                        case SemanticConventions.AttributeMessagingOperationType:
                            operationType = tag.Value?.ToString();
                            break;

                        case SemanticConventions.AttributeMessagingSystem:
                            system = tag.Value?.ToString();
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        Assert.Equal(expectedOperationName, operationName);
        Assert.Equal(expectedOperationType, operationType);
        Assert.Equal(ConfluentKafkaCommon.KafkaMessagingSystem, system);
    }
}
