// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class OpenTelemetryConsumeResultExtensionsTests
{
    [Fact]
    public void TryExtractPropagationContext_NullConsumeResult_ThrowsArgumentNullException()
    {
        ConsumeResult<string, string> consumeResult = null!;

        Assert.Throws<ArgumentNullException>(() =>
            consumeResult.TryExtractPropagationContext(out _));
    }

    [Fact]
    public void TryExtractPropagationContext_NullMessage_ReturnsTrue()
    {
        var consumeResult = new ConsumeResult<string, string> { Message = null };

        var result = consumeResult.TryExtractPropagationContext(out var propagationContext);

        Assert.True(result);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public void TryExtractPropagationContext_NullHeaders_ReturnsTrue()
    {
        var consumeResult = new ConsumeResult<string, string>
        {
            Message = new Message<string, string> { Value = "v", Headers = null },
        };

        var result = consumeResult.TryExtractPropagationContext(out var propagationContext);

        Assert.True(result);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public void TryExtractPropagationContext_ValidTraceparentHeader_ReturnsTrueWithValidContext()
    {
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        const string spanId = "b7ad6b7169203331";
        const string traceparent = $"00-{traceId}-{spanId}-01";

        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(traceparent) },
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = new Message<string, string> { Value = "v", Headers = headers },
        };

        // TracerProvider registers the W3C TraceContext propagator as the default
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .Build();

        var result = consumeResult.TryExtractPropagationContext(out var propagationContext);

        Assert.True(result);
        Assert.True(propagationContext.ActivityContext.IsValid());
        Assert.Equal(traceId, propagationContext.ActivityContext.TraceId.ToHexString());
        Assert.Equal(spanId, propagationContext.ActivityContext.SpanId.ToHexString());
    }

    [Fact]
    public void TryExtractPropagationContext_EmptyHeaders_ReturnsTrue()
    {
        var consumeResult = new ConsumeResult<string, string>
        {
            Message = new Message<string, string> { Value = "v", Headers = [] },
        };

        var result = consumeResult.TryExtractPropagationContext(out var propagationContext);

        Assert.True(result);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_NullConsumer_ThrowsArgumentNullException()
    {
        IConsumer<string, string> consumer = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            consumer.ConsumeAndProcessMessageAsync(NoOpHandler).AsTask());
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_NullHandler_ThrowsArgumentNullException()
    {
        var consumer = BuildInstrumentedConsumer(new ConsumeResult<string, string>
        {
            Topic = "t",
            Message = new Message<string, string> { Value = "v" },
        });

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            consumer.ConsumeAndProcessMessageAsync(null!).AsTask());
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_NonInstrumentedConsumer_ThrowsArgumentException()
    {
        var plainConsumer = new FakeConsumer<string, string>();

        await Assert.ThrowsAsync<ArgumentException>(
            "consumer",
            () => plainConsumer.ConsumeAndProcessMessageAsync(NoOpHandler).AsTask());
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_NullConsumeResult_ReturnsNull()
    {
        var consumer = BuildInstrumentedConsumer<string, string>(null);

        var result = await consumer.ConsumeAndProcessMessageAsync(NoOpHandler);

        Assert.Null(result);
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_PartitionEof_ReturnsWithoutInvokingHandler()
    {
        var consumeResult = new ConsumeResult<string, string> { IsPartitionEOF = true };
        var consumer = BuildInstrumentedConsumer(consumeResult);
        var handlerInvoked = false;

        var result = await consumer.ConsumeAndProcessMessageAsync((_, _, _) =>
        {
            handlerInvoked = true;
            return new ValueTask(Task.CompletedTask);
        });

        Assert.False(handlerInvoked);
        Assert.Same(consumeResult, result);
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_HandlerThrows_SetsErrorStatusAndRethrows()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                                       .AddSource(ConfluentKafkaCommon.InstrumentationName)
                                       .AddInMemoryExporter(activities)
                                       .Build())
        {
            var consumer = BuildInstrumentedConsumer(new ConsumeResult<string, string>
            {
                Topic = "error-topic",
                Partition = new Partition(0),
                Offset = new Offset(1),
                Message = new Message<string, string> { Value = "v" },
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                consumer.ConsumeAndProcessMessageAsync((_, _, _) =>
                    throw new InvalidOperationException("processing failed")).AsTask());

            tracerProvider.ForceFlush();
        }

        var processActivity = Assert.Single(activities, a => a.DisplayName.EndsWith("process", StringComparison.Ordinal));
        Assert.Equal(ActivityStatusCode.Error, processActivity.Status);
    }

    [Fact]
    public async Task ConsumeAndProcessMessageAsync_ValidMessage_CreatesProcessActivity()
    {
        var activities = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                                       .AddSource(ConfluentKafkaCommon.InstrumentationName)
                                       .AddInMemoryExporter(activities)
                                       .Build())
        {
            var consumer = BuildInstrumentedConsumer(new ConsumeResult<string, string>
            {
                Topic = "process-topic",
                Partition = new Partition(0),
                Offset = new Offset(5),
                Message = new Message<string, string> { Value = "v" },
            });

            await consumer.ConsumeAndProcessMessageAsync(NoOpHandler);

            tracerProvider.ForceFlush();
        }

        var processActivity = Assert.Single(activities, a => a.DisplayName == "process-topic process");

        Assert.Equal(ActivityKind.Consumer, processActivity.Kind);
        Assert.Equal("kafka", processActivity.GetTagValue(SemanticConventions.AttributeMessagingSystem));
        Assert.Equal("process", processActivity.GetTagValue(SemanticConventions.AttributeMessagingOperation));
    }

    private static InstrumentedConsumer<TKey, TValue> BuildInstrumentedConsumer<TKey, TValue>(
        ConsumeResult<TKey, TValue>? consumeResult)
    {
        var fake = new FakeConsumer<TKey, TValue> { ConsumeResult = consumeResult };
        var options = new ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>
        {
            Traces = true,
            Metrics = false,
        };

        return new(fake, options) { GroupId = "test-group" };
    }

    private static ValueTask NoOpHandler<TKey, TValue>(
        ConsumeResult<TKey, TValue> result,
        Activity? activity,
        CancellationToken ct) => new(Task.CompletedTask);

    private sealed class FakeConsumer<TKey, TValue> : IConsumer<TKey, TValue>
    {
        public ConsumeResult<TKey, TValue>? ConsumeResult { get; set; }

        public Handle Handle => null!;

        public string Name => "fake-consumer";

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

        public List<TopicPartitionOffset> OffsetsForTimes(
            IEnumerable<TopicPartitionTimestamp> timestampsToSearch,
            TimeSpan timeout) => [];

        public WatermarkOffsets GetWatermarkOffsets(TopicPartition topicPartition) =>
            new(Offset.Unset, Offset.Unset);

        public WatermarkOffsets QueryWatermarkOffsets(TopicPartition topicPartition, TimeSpan timeout) =>
            new(Offset.Unset, Offset.Unset);

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
