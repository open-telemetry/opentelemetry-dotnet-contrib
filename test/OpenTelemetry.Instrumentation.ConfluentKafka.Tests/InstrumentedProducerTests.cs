// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Confluent.Kafka;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class InstrumentedProducerTests
{
    [Fact]
    public async Task ProduceAsync_Topic_CreatesActivityWithCorrectTags()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeProducer = new FakeProducer<string, string>();
            var options = new ConfluentKafkaProducerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedProducer = new InstrumentedProducer<string, string>(fakeProducer, options);

            await instrumentedProducer.ProduceAsync("unit-test-topic", new Message<string, string> { Value = "hello" });

            tracerProvider.ForceFlush();
        }

        var activity = activities.Single(a => a.DisplayName == "unit-test-topic publish");

        Assert.Equal(ActivityKind.Producer, activity.Kind);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("publish", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("unit-test-topic", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
    }

    [Fact]
    public async Task ProduceAsync_TopicPartition_SetsPartitionTag()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeProducer = new FakeProducer<string, string>();
            var options = new ConfluentKafkaProducerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedProducer = new InstrumentedProducer<string, string>(fakeProducer, options);

            await instrumentedProducer.ProduceAsync(
                new TopicPartition("partition-topic", new Partition(3)),
                new Message<string, string> { Key = "msg-key", Value = "hello" });

            tracerProvider.ForceFlush();
        }

        var activity = activities.Single(a => a.DisplayName == "partition-topic publish");
        Assert.Equal(3, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaDestinationPartition));
        Assert.Equal("msg-key", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public async Task ProduceAsync_TracesDisabled_NoActivityCreated()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeProducer = new FakeProducer<string, string>();
            var options = new ConfluentKafkaProducerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = false,
            };
            var instrumentedProducer = new InstrumentedProducer<string, string>(fakeProducer, options);

            await instrumentedProducer.ProduceAsync("disabled-traces-topic", new Message<string, string> { Value = "hello" });

            tracerProvider.ForceFlush();
        }

        Assert.DoesNotContain(activities, a => a.DisplayName == "disabled-traces-topic publish");
    }

    [Fact]
    public async Task ProduceAsync_WhenProducerThrows_SetsErrorStatusAndRethrows()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeProducer = new FakeProducer<string, string>
            {
                ThrowArgumentException = new ArgumentException("bad topic", "topic"),
            };
            var options = new ConfluentKafkaProducerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedProducer = new InstrumentedProducer<string, string>(fakeProducer, options);

            await Assert.ThrowsAsync<ArgumentException>(
                () => instrumentedProducer.ProduceAsync("error-topic", new Message<string, string> { Value = "hello" }));

            tracerProvider.ForceFlush();
        }

        var activity = activities.Single(a => a.DisplayName == "error-topic publish");

        Assert.Equal(ActivityStatusCode.Error, activity.Status);

        var errorType = activity.GetTagValue(SemanticConventions.AttributeErrorType)?.ToString();
        Assert.NotNull(errorType);
        Assert.Contains("ArgumentException", errorType);
    }

    [Fact]
    public async Task ProduceAsync_InjectsTraceContextIntoMessageHeaders()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .Build();

        var fakeProducer = new FakeProducer<string, string>();
        var options = new ConfluentKafkaProducerInstrumentationOptions<string, string>
        {
            Traces = true,
            Metrics = false,
        };
        var instrumentedProducer = new InstrumentedProducer<string, string>(fakeProducer, options);

        var message = new Message<string, string> { Value = "hello" };
        await instrumentedProducer.ProduceAsync("header-inject-topic", message);

        // Trace context should have been injected into headers
        Assert.NotNull(message.Headers);
        Assert.True(message.Headers.Count > 0, "Expected trace context headers to be injected.");
        Assert.Contains(message.Headers, h => h.Key == "traceparent");
    }

    [Fact]
    public void ProduceSync_Topic_CreatesActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeProducer = new FakeProducer<string, string>();
            var options = new ConfluentKafkaProducerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedProducer = new InstrumentedProducer<string, string>(fakeProducer, options);

            instrumentedProducer.Produce("sync-topic", new Message<string, string> { Value = "hello" });

            tracerProvider.ForceFlush();
        }

        Assert.Contains(activities, a => a.DisplayName == "sync-topic publish");
    }

    private sealed class FakeProducer<TKey, TValue> : IProducer<TKey, TValue>
    {
        public ArgumentException? ThrowArgumentException { get; set; }

        public Handle Handle => null!;

        public string Name => "fake-producer-1";

        public int AddBrokers(string brokers) => 0;

        public void SetSaslCredentials(string username, string password)
        {
            // No-op
        }

        public Task<DeliveryResult<TKey, TValue>> ProduceAsync(string topic, Message<TKey, TValue> message, CancellationToken cancellationToken = default) =>
            this.ThrowArgumentException != null
                ? throw this.ThrowArgumentException
                : Task.FromResult(new DeliveryResult<TKey, TValue>
                {
                    Topic = topic,
                    Status = PersistenceStatus.Persisted,
                });

        public Task<DeliveryResult<TKey, TValue>> ProduceAsync(TopicPartition topicPartition, Message<TKey, TValue> message, CancellationToken cancellationToken = default) =>
            this.ThrowArgumentException != null
                ? throw this.ThrowArgumentException
                : Task.FromResult(new DeliveryResult<TKey, TValue>
                {
                    Topic = topicPartition.Topic,
                    Status = PersistenceStatus.Persisted,
                });

        public void Produce(string topic, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
        {
            if (this.ThrowArgumentException != null)
            {
                throw this.ThrowArgumentException;
            }
        }

        public void Produce(TopicPartition topicPartition, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
        {
            if (this.ThrowArgumentException != null)
            {
                throw this.ThrowArgumentException;
            }
        }

        public int Poll(TimeSpan timeout) => 0;

        public int Flush(TimeSpan timeout) => 0;

        public void Flush(CancellationToken cancellationToken = default)
        {
            // No-op
        }

        public void InitTransactions(TimeSpan timeout)
        {
            // No-op
        }

        public void BeginTransaction()
        {
            // No-op
        }

        public void CommitTransaction(TimeSpan timeout)
        {
            // No-op
        }

        public void CommitTransaction()
        {
            // No-op
        }

        public void AbortTransaction(TimeSpan timeout)
        {
            // No-op
        }

        public void AbortTransaction()
        {
            // No-op
        }

        public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
