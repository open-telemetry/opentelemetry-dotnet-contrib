// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Confluent.Kafka;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class PropagationTests
{
    private readonly ITestOutputHelper outputHelper;

    public PropagationTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper;
    }

    [Fact]
    public async Task InjectExtractContractTest()
    {
        ProducerConfig producerConfig = new ProducerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
        };
        InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);
        ConsumerConfig consumerConfig = new ConsumerConfig
        {
            BootstrapServers = KafkaHelpers.KafkaEndPoint,
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = true,
        };
        InstrumentedConsumerBuilder<string, string> consumerBuilder = new(consumerConfig);
        var sampler = new TestSampler();
        var activities = new List<Activity>();
        string topic = $"otel-topic-{Guid.NewGuid()}";
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(activities)
            .SetSampler(sampler)
            .AddKafkaProducerInstrumentation(producerBuilder)
            .AddKafkaConsumerInstrumentation(consumerBuilder)
            .AddSource("TestActivitySource")
            .Build();

        IProducer<string, string> producer = producerBuilder.Build();

        (string key, string value) = ("key1", "value1");

        using (ActivitySource activitySource = new ActivitySource("TestActivitySource"))
        {
            using var activity = activitySource.StartActivity(name: "TestActivity");
            activity?.SetBaggage(key, value);
            await producer.ProduceAsync(topic, new Message<string, string>
            {
                Value = "any_value",
            });
        }

        IConsumer<string, string> consumer = consumerBuilder.Build();
        consumer.Subscribe(topic);
        while (true)
        {
            ConsumeResult<string, string> consumeResult = consumer.Consume();
            if (consumeResult == null)
            {
                continue;
            }

            if (consumeResult.IsPartitionEOF)
            {
                break;
            }

            Assert.NotNull(Activity.Current.ParentId);
            Assert.Equal("value1", Activity.Current.GetBaggageItem("key1"));

            this.outputHelper.WriteLine($"Consumed message '{consumeResult.Message.Value}' at: '{consumeResult.TopicPartitionOffset}'.");
        }
    }
}
