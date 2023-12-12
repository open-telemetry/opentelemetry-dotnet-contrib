// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Trace;

ProducerConfig producerConfig = new();
producerConfig.BootstrapServers = "localhost:9092";
InstrumentedProducerBuilder<string, string> producerBuilder = new(producerConfig);

ConsumerConfig consumerConfigA = new();
consumerConfigA.BootstrapServers = "localhost:9092";
consumerConfigA.GroupId = "group-a";
consumerConfigA.AutoOffsetReset = AutoOffsetReset.Earliest;
consumerConfigA.EnablePartitionEof = true;
InstrumentedConsumerBuilder<string, string> consumerBuilderA = new(consumerConfigA);

ConsumerConfig consumerConfigB = new();
consumerConfigB.BootstrapServers = "localhost:9092";
consumerConfigB.GroupId = "group-b";
consumerConfigB.AutoOffsetReset = AutoOffsetReset.Earliest;
consumerConfigB.EnablePartitionEof = true;
InstrumentedConsumerBuilder<string, string> consumerBuilderB = new(consumerConfigB);

string topic = $"test-topic-{Guid.NewGuid()}";

// Configure exporter to export traces to Zipkin
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddConsoleExporter()
    .AddZipkinExporter()
    .AddKafkaProducerInstrumentation(producerBuilder)
    .AddKafkaConsumerInstrumentation("consumer-a", consumerBuilderA)
    .AddKafkaConsumerInstrumentation("consumer-b", consumerBuilderB)
    .Build();

// produce a message to the topic "test-topic" with key "any_key" and value "any_value".
IProducer<string, string> producer = producerBuilder.Build();
await producer.ProduceAsync(topic, new Message<string, string>
{
    Key = "any_key",
    Value = "any_value",
});

tracerProvider.ForceFlush();

IConsumer<string, string> consumerA = consumerBuilderA.Build();
consumerA.Subscribe(topic);
while (true)
{
    ConsumeResult<string, string> consumeResult = consumerA.Consume();
    if (consumeResult == null)
    {
        continue;
    }

    if (consumeResult.IsPartitionEOF)
    {
        break;
    }

    Console.WriteLine($"Consumer A received message: {consumeResult.Message.Value}");
}

consumerA.Close();

tracerProvider.ForceFlush();

IConsumer<string, string> consumerB = consumerBuilderB.Build();
consumerB.Subscribe(topic);
while (true)
{
    ConsumeResult<string, string> consumeResult = consumerB.Consume();
    if (consumeResult == null)
    {
        continue;
    }

    if (consumeResult.IsPartitionEOF)
    {
        break;
    }

    Console.WriteLine($"Consumer B received message: {consumeResult.Message.Value}");
}

consumerB.Close();

tracerProvider.ForceFlush();

// Wait for the exporter to finish sending
await Task.Delay(TimeSpan.FromSeconds(5));

Console.WriteLine("done!");
