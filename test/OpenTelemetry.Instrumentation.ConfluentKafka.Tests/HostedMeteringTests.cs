// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection("Kafka")]
public class HostedMeteringTests
{
    private readonly ITestOutputHelper outputHelper;

    public HostedMeteringTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
    }

    [Trait("CategoryName", "KafkaIntegrationTests")]
    [SkipUnlessEnvVarFoundFact(KafkaHelpers.KafkaEndPointEnvVarName)]
    public async Task ResolveInstrumentedBuildersFromServiceProviderTest()
    {
        List<Metric> metrics = new();
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<InstrumentedProducerBuilder<string, string>>(_ =>
                new InstrumentedProducerBuilder<string, string>(new ProducerConfig()
                {
                    BootstrapServers = KafkaHelpers.KafkaEndPoint,
                }));
            services.AddSingleton<InstrumentedConsumerBuilder<string, string>>(_ =>
                new InstrumentedConsumerBuilder<string, string>(new ConsumerConfig()
                {
                    BootstrapServers = KafkaHelpers.KafkaEndPoint,
                    GroupId = Guid.NewGuid().ToString(),
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnablePartitionEof = true,
                }));

            services.AddOpenTelemetry().WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .AddInMemoryExporter(metrics)
                    .AddKafkaProducerInstrumentation<string, string>()
                    .AddKafkaConsumerInstrumentation<string, string>();
            });
        });

        using var host = builder.Build();

        await host.StartAsync();

        string topic = $"otel-topic-{Guid.NewGuid()}";
        var producer = host.Services.GetRequiredService<InstrumentedProducerBuilder<string, string>>().Build();

        for (int i = 0; i < 100; i++)
        {
            producer.Produce(topic, new Message<string, string>()
            {
                Key = $"any_key_{i}",
                Value = $"any_value_{i}",
            });
            this.outputHelper.WriteLine("produced message {0}", i);
        }

        producer.Flush();

        await Task.Delay(1100);

        producer.Poll(TimeSpan.FromSeconds(1));

        host.Services.GetRequiredService<MeterProvider>().ForceFlush();

        var groups = from m in metrics
            group m by m.Name into g
            select new { MetricName = g.Key, Count = g.Count() };

        Assert.Equal(11, groups.Count());

        metrics.Clear();

        var consumer = host.Services.GetRequiredService<InstrumentedConsumerBuilder<string, string>>().Build();
        consumer.Subscribe(topic);

        int j = 0;
        while (true)
        {
            var consumerResult = consumer.Consume();
            if (consumerResult == null)
            {
                continue;
            }

            if (consumerResult.IsPartitionEOF)
            {
                break;
            }

            this.outputHelper.WriteLine("consumed message {0}", j);
            j++;
        }

        host.Services.GetRequiredService<MeterProvider>().ForceFlush();

        groups = from m in metrics
            group m by m.Name into g
            select new { MetricName = g.Key, Count = g.Count() };

        Assert.Equal(11, groups.Count());

        await host.StopAsync();
    }
}
