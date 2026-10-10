// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection(KafkaCollection.Name)]
[Trait("CategoryName", "KafkaIntegrationTests")]
public class ProducerClientMetricsIntegrationTests(KafkaFixture fixture)
{
    [EnabledOnDockerPlatformFact(DockerPlatform.Linux)]
    public async Task ProducerStatisticsAndExistingOperationMetricsAreExported()
    {
        var metrics = new List<Metric>();
        var builder = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = fixture.TypedContainer.GetConnectionString(),
        }).AsInstrumentedProducerBuilder(new ConfluentKafkaInstrumentedProducerBuilderOptions
        {
            EnableClientMetrics = true,
            StatisticsInterval = TimeSpan.FromSeconds(1),
        });
        var callbacks = 0;
        builder.SetStatisticsHandler((_, _) => Interlocked.Increment(ref callbacks));
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddKafkaProducerInstrumentation(builder)
            .AddInMemoryExporter(metrics)
            .Build();
        using var producer = builder.Build();
        const int MessageCount = 5;
        var topic = $"otel-client-metrics-{Guid.NewGuid()}";
        for (var i = 0; i < MessageCount; i++)
        {
            await producer.ProduceAsync(topic, new Message<string, string> { Value = "hello" }, TestContext.Current.CancellationToken);
        }

        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            metrics.Clear();
            provider.ForceFlush();
            if (GetCounter(metrics, "kafka.producer.record_send_total") >= MessageCount)
            {
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(Volatile.Read(ref callbacks) > 0);
        Assert.Equal(MessageCount, GetCounter(metrics, "kafka.producer.record_send_total"));
        Assert.True(GetCounter(metrics, "kafka.producer.byte_total") > 0);
        Assert.Equal(MessageCount, GetCounter(metrics, SemanticConventions.MetricMessagingClientSentMessages));
        Assert.Contains(metrics, metric => metric.Name == SemanticConventions.MetricMessagingClientOperationDuration);
        Assert.Contains(metrics, metric => metric.Name == "kafka.producer.buffer_total_bytes");
        Assert.All(metrics.Where(metric => metric.Name.StartsWith("kafka.producer.", StringComparison.Ordinal)), metric =>
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                Assert.Equal(0, point.Tags.Count);
            }
        });
    }

    private static long GetCounter(IEnumerable<Metric> metrics, string name)
    {
        long total = 0;
        foreach (var metric in metrics.Where(metric => metric.Name == name))
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                total += point.GetSumLong();
            }
        }

        return total;
    }
}
