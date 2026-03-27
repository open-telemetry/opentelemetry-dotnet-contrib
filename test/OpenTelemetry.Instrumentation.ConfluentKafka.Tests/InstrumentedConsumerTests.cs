// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class InstrumentedConsumerTests
{
    [Fact]
    public void Consume_CancellationToken_CreatesActivityWithCorrectTags()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "consume-topic",
                    Partition = new Partition(1),
                    Offset = new Offset(42),
                    Message = new Message<string, string> { Key = "msg-key", Value = "msg-value" },
                },
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options)
            {
                GroupId = "test-group",
            };

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        var activity = activities.Single(a => a.DisplayName == "consume-topic receive");
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal("kafka", activity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("receive", activity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
        Assert.Equal("consume-topic", activity.GetTagValue(SemanticConventions.AttributeMessagingDestinationName));
        Assert.Equal(42L, activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageOffset));
        Assert.Equal("test-group", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaConsumerGroup));
        Assert.Equal("msg-key", activity.GetTagValue(SemanticConventions.AttributeMessagingKafkaMessageKey));
    }

    [Fact]
    public void Consume_TracesDisabled_NoActivityCreated()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "disabled-traces-topic",
                    Partition = new Partition(0),
                    Offset = new Offset(1),
                    Message = new Message<string, string> { Value = "msg-value" },
                },
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = false,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        Assert.DoesNotContain(activities, a => a.DisplayName == "disabled-traces-topic receive");
    }

    [Fact]
    public void Consume_PartitionEof_DoesNotCreateActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "eof-topic",
                    Partition = new Partition(0),
                    Offset = Offset.End,
                    IsPartitionEOF = true,  // EOF — should be skipped
                },
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        Assert.DoesNotContain(activities, a => a.DisplayName == "eof-topic receive");
    }

    [Fact]
    public void Consume_WithPropagatedTraceContext_LinksToProducerActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInMemoryExporter(activities)
            .Build())
        {
            // A well-formed traceparent header representing a remote producer span
            const string traceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
            var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(traceparent) },
        };

            var fakeConsumer = new FakeConsumer<string, string>
            {
                ConsumeResult = new ConsumeResult<string, string>
                {
                    Topic = "linked-topic",
                    Partition = new Partition(0),
                    Offset = new Offset(10),
                    Message = new Message<string, string> { Value = "value", Headers = headers },
                },
            };

            var options = new ConfluentKafkaConsumerInstrumentationOptions<string, string>
            {
                Traces = true,
                Metrics = false,
            };
            var instrumentedConsumer = new InstrumentedConsumer<string, string>(fakeConsumer, options);

            _ = instrumentedConsumer.Consume(CancellationToken.None);

            tracerProvider.ForceFlush();
        }

        var snapshot = activities.ToList();
        var activity = snapshot.Single(a => a.DisplayName == "linked-topic receive");

        // The extracted producer context should be linked (not parented) per messaging conventions
        Assert.NotEmpty(activity.Links);
        Assert.Equal(
            "0af7651916cd43dd8448eb211c80319c",
            activity.Links.First().Context.TraceId.ToHexString());
    }

    private sealed class FakeConsumer<TKey, TValue> : IConsumer<TKey, TValue>
    {
        public ConsumeResult<TKey, TValue>? ConsumeResult { get; set; }

        public Handle Handle => null!;

        public string Name => "fake-consumer-1";

        public string MemberId => string.Empty;

        public List<TopicPartition> Assignment => [];

        public List<string> Subscription => [];

        public IConsumerGroupMetadata ConsumerGroupMetadata => null!;

        public int AddBrokers(string brokers) => 0;

        public void SetSaslCredentials(string username, string password)
        {
            // No-op
        }

        public ConsumeResult<TKey, TValue>? Consume(int millisecondsTimeout) => this.ConsumeResult;

        public ConsumeResult<TKey, TValue>? Consume(CancellationToken cancellationToken = default) => this.ConsumeResult;

        public ConsumeResult<TKey, TValue>? Consume(TimeSpan timeout) => this.ConsumeResult;

        public void Subscribe(IEnumerable<string> topics)
        {
            // No-op
        }

        public void Subscribe(string topic)
        {
            // No-op
        }

        public void Unsubscribe()
        {
            // No-op
        }

        public void Assign(TopicPartition partition)
        {
            // No-op
        }

        public void Assign(TopicPartitionOffset partition)
        {
            // No-op
        }

        public void Assign(IEnumerable<TopicPartitionOffset> partitions)
        {
            // No-op
        }

        public void Assign(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
        {
            // No-op
        }

        public void IncrementalAssign(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void Unassign()
        {
            // No-op
        }

        public void StoreOffset(ConsumeResult<TKey, TValue> result)
        {
            // No-op
        }

        public void StoreOffset(TopicPartitionOffset offset)
        {
            // No-op
        }

        public List<TopicPartitionOffset> Commit() => [];

        public void Commit(IEnumerable<TopicPartitionOffset> offsets)
        {
            // No-op
        }

        public void Commit(ConsumeResult<TKey, TValue> result)
        {
            // No-op
        }

        public void Seek(TopicPartitionOffset tpo)
        {
            // No-op
        }

        public void Pause(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public void Resume(IEnumerable<TopicPartition> partitions)
        {
            // No-op
        }

        public List<TopicPartitionOffset> Committed(TimeSpan timeout) => [];

        public List<TopicPartitionOffset> Committed(IEnumerable<TopicPartition> partitions, TimeSpan timeout) => [];

        public Offset Position(TopicPartition partition) => Offset.Unset;

        public List<TopicPartitionOffset> OffsetsForTimes(IEnumerable<TopicPartitionTimestamp> timestampsToSearch, TimeSpan timeout) => [];

        public WatermarkOffsets GetWatermarkOffsets(TopicPartition topicPartition) => new(Offset.Unset, Offset.Unset);

        public WatermarkOffsets QueryWatermarkOffsets(TopicPartition topicPartition, TimeSpan timeout) => new(Offset.Unset, Offset.Unset);

        public void Close()
        {
            // No-op
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
