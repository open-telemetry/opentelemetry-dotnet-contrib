// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Confluent.Kafka;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class InstrumentedConsumer<TKey, TValue> : IConsumer<TKey, TValue>
{
    private readonly IConsumer<TKey, TValue> consumer;
    private readonly ConsumerConfig config;

    public InstrumentedConsumer(IConsumer<TKey, TValue> consumer, ConsumerConfig config)
    {
        this.consumer = consumer;
        this.config = config;
    }

    public Handle Handle => this.consumer.Handle;

    public string Name => this.consumer.Name;

    public string MemberId => this.consumer.MemberId;

    public List<TopicPartition> Assignment => this.consumer.Assignment;

    public List<string> Subscription => this.consumer.Subscription;

    public IConsumerGroupMetadata ConsumerGroupMetadata => this.consumer.ConsumerGroupMetadata;

    public ConsumerConfig Config => this.config;

    public void Dispose()
    {
        this.consumer.Dispose();
    }

    public int AddBrokers(string brokers)
    {
        return this.consumer.AddBrokers(brokers);
    }

    public void SetSaslCredentials(string username, string password)
    {
        this.consumer.SetSaslCredentials(username, password);
    }

    public ConsumeResult<TKey, TValue>? Consume(int millisecondsTimeout) =>
        this.Consume(TimeSpan.FromMilliseconds(millisecondsTimeout));

    public ConsumeResult<TKey, TValue>? Consume(CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.UtcNow;
        ConsumeResult<TKey, TValue>? result = null;
        ConsumeResult consumeResult = default;
        string? errorType = null;
        using var activity = this.StartReceiveActivity();
        try
        {
            result = this.consumer.Consume(cancellationToken);
            consumeResult = ExtractConsumeResult(result);
            UpdateActivityWithConsumeResults(activity, result);
            return result;
        }
        catch (ConsumeException e)
        {
            (consumeResult, errorType) = ExtractConsumeResult(e);
            activity?.AddException(e);
            activity?.SetStatus(ActivityStatusCode.Error);

            throw;
        }
        finally
        {
            var end = DateTimeOffset.UtcNow;
            if (result is { IsPartitionEOF: false })
            {
                InstrumentConsumption(start, end, consumeResult, errorType);
            }
        }
    }

    public ConsumeResult<TKey, TValue>? Consume(TimeSpan timeout)
    {
        var start = DateTimeOffset.UtcNow;
        ConsumeResult<TKey, TValue>? result = null;
        ConsumeResult consumeResult = default;
        string? errorType = null;
        using var activity = this.StartReceiveActivity();
        try
        {
            result = this.consumer.Consume(timeout);
            consumeResult = ExtractConsumeResult(result);
            UpdateActivityWithConsumeResults(activity, result);
            return result;
        }
        catch (ConsumeException e)
        {
            (consumeResult, errorType) = ExtractConsumeResult(e);
            activity?.AddException(e);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            var end = DateTimeOffset.UtcNow;
            if (result is { IsPartitionEOF: false })
            {
                InstrumentConsumption(start, end, consumeResult, errorType);
            }
        }
    }

    public void Subscribe(IEnumerable<string> topics)
    {
        this.consumer.Subscribe(topics);
    }

    public void Subscribe(string topic)
    {
        this.consumer.Subscribe(topic);
    }

    public void Unsubscribe()
    {
        this.consumer.Unsubscribe();
    }

    public void Assign(TopicPartition partition)
    {
        this.consumer.Assign(partition);
    }

    public void Assign(TopicPartitionOffset partition)
    {
        this.consumer.Assign(partition);
    }

    public void Assign(IEnumerable<TopicPartitionOffset> partitions)
    {
        this.consumer.Assign(partitions);
    }

    public void Assign(IEnumerable<TopicPartition> partitions)
    {
        this.consumer.Assign(partitions);
    }

    public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        this.consumer.IncrementalAssign(partitions);
    }

    public void IncrementalAssign(IEnumerable<TopicPartition> partitions)
    {
        this.consumer.IncrementalAssign(partitions);
    }

    public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        this.consumer.IncrementalUnassign(partitions);
    }

    public void Unassign()
    {
        this.consumer.Unassign();
    }

    public void StoreOffset(ConsumeResult<TKey, TValue> result)
    {
        this.consumer.StoreOffset(result);
    }

    public void StoreOffset(TopicPartitionOffset offset)
    {
        this.consumer.StoreOffset(offset);
    }

    public List<TopicPartitionOffset> Commit()
    {
        return this.consumer.Commit();
    }

    public void Commit(IEnumerable<TopicPartitionOffset> offsets)
    {
        this.consumer.Commit(offsets);
    }

    public void Commit(ConsumeResult<TKey, TValue> result)
    {
        this.consumer.Commit(result);
    }

    public void Seek(TopicPartitionOffset tpo)
    {
        this.consumer.Seek(tpo);
    }

    public void Pause(IEnumerable<TopicPartition> partitions)
    {
        this.consumer.Pause(partitions);
    }

    public void Resume(IEnumerable<TopicPartition> partitions)
    {
        this.consumer.Resume(partitions);
    }

    public List<TopicPartitionOffset> Committed(TimeSpan timeout)
    {
        return this.consumer.Committed(timeout);
    }

    public List<TopicPartitionOffset> Committed(IEnumerable<TopicPartition> partitions, TimeSpan timeout)
    {
        return this.consumer.Committed(partitions, timeout);
    }

    public Offset Position(TopicPartition partition)
    {
        return this.consumer.Position(partition);
    }

    public List<TopicPartitionOffset> OffsetsForTimes(IEnumerable<TopicPartitionTimestamp> timestampsToSearch, TimeSpan timeout)
    {
        return this.consumer.OffsetsForTimes(timestampsToSearch, timeout);
    }

    public WatermarkOffsets GetWatermarkOffsets(TopicPartition topicPartition)
    {
        return this.consumer.GetWatermarkOffsets(topicPartition);
    }

    public WatermarkOffsets QueryWatermarkOffsets(TopicPartition topicPartition, TimeSpan timeout)
    {
        return this.consumer.QueryWatermarkOffsets(topicPartition, timeout);
    }

    public void Close()
    {
        this.consumer.Close();
    }

    private static string FormatConsumeException(ConsumeException consumeException) =>
        $"ConsumeException: {consumeException.Error}";

    private static ConsumeResult ExtractConsumeResult(ConsumeResult<TKey, TValue> result) => result switch
    {
        null => new ConsumeResult(null, null),
        { Message: null } => new ConsumeResult(result.TopicPartitionOffset, null),
        _ => new ConsumeResult(result.TopicPartitionOffset, result.Message.Headers, result.Message.Key),
    };

    private static (ConsumeResult ConsumeResult, string ErrorType) ExtractConsumeResult(ConsumeException exception) => exception switch
    {
        { ConsumerRecord: null } => (new ConsumeResult(null, null), FormatConsumeException(exception)),
        { ConsumerRecord.Message: null } => (new ConsumeResult(exception.ConsumerRecord.TopicPartitionOffset, null), FormatConsumeException(exception)),
        _ => (new ConsumeResult(exception.ConsumerRecord.TopicPartitionOffset, exception.ConsumerRecord.Message.Headers, exception.ConsumerRecord.Message.Key), FormatConsumeException(exception)),
    };

    private static void GetTags(string topic, out TagList tags, int? partition = null, string? errorType = null)
    {
        tags = new TagList()
        {
            new KeyValuePair<string, object?>(
                SemanticConventions.AttributeMessagingOperation,
                ConfluentKafkaCommon.ReceiveOperationName),
            new KeyValuePair<string, object?>(
                SemanticConventions.AttributeMessagingSystem,
                ConfluentKafkaCommon.KafkaMessagingSystem),
            new KeyValuePair<string, object?>(
                SemanticConventions.AttributeMessagingDestinationName,
                topic),
        };

        if (partition is not null)
        {
            tags.Add(
                new KeyValuePair<string, object?>(
                    SemanticConventions.AttributeMessagingKafkaDestinationPartition,
                    partition));
        }

        if (errorType is not null)
        {
            tags.Add(
                new KeyValuePair<string, object?>(
                    SemanticConventions.AttributeErrorType,
                    errorType));
        }
    }

    private static void UpdateActivityWithConsumeResults(Activity? activity, ConsumeResult<TKey, TValue>? consumeResult)
    {
        if (activity == null || consumeResult == null)
        {
            return;
        }

        if (!consumeResult.IsPartitionEOF)
        {
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaDestinationPartition, consumeResult.TopicPartition.Partition.Value);
            activity.SetTag(SemanticConventions.AttributeMessagingDestinationName, consumeResult.Topic);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaMessageOffset, consumeResult.Offset.Value);
        }
    }

    private static void RecordReceive(TopicPartition topicPartition, TimeSpan duration, string? errorType = null)
    {
        if (!ConfluentKafkaCommon.ReceiveMessagesCounter.Enabled &&
            !ConfluentKafkaCommon.ReceiveDurationHistogram.Enabled)
        {
            return;
        }

        GetTags(topicPartition.Topic, out var tags, partition: topicPartition.Partition, errorType);

        ConfluentKafkaCommon.ReceiveMessagesCounter.Add(1, in tags);
        ConfluentKafkaCommon.ReceiveDurationHistogram.Record(duration.TotalSeconds, in tags);
    }

    private static void InstrumentConsumption(DateTimeOffset startTime, DateTimeOffset endTime, ConsumeResult consumeResult, string? errorType)
    {
        var duration = endTime - startTime;
        RecordReceive(consumeResult.TopicPartitionOffset!.TopicPartition, duration, errorType);
    }

    private Activity? StartReceiveActivity()
    {
        if (!ConfluentKafkaCommon.ConsumerActivitySource.HasListeners())
        {
            return null;
        }

        var spanName = $"{ConfluentKafkaCommon.ReceiveOperationName} {this.config.BootstrapServers}";

        var tags = default(TagList);
        tags.Add(SemanticConventions.AttributeMessagingSystem, ConfluentKafkaCommon.KafkaMessagingSystem);
        tags.Add(SemanticConventions.AttributeMessagingClientId, this.Name);
        tags.Add(SemanticConventions.AttributeMessagingKafkaConsumerGroup, this.config.GroupId);
        tags.Add(SemanticConventions.AttributeMessagingDestinationName, this.config.BootstrapServers);
        tags.Add(SemanticConventions.AttributeMessagingOperation, ConfluentKafkaCommon.ReceiveOperationName);


        var activity = ConfluentKafkaCommon.ConsumerActivitySource.StartActivity(spanName, kind: ActivityKind.Consumer, parentContext: default, tags: tags);

        return activity;
    }

    private readonly record struct ConsumeResult(
        TopicPartitionOffset? TopicPartitionOffset,
        Headers? Headers,
        object? Key = null)
    {
        public object? Key { get; } = Key;

        public Headers? Headers { get; } = Headers;

        public TopicPartitionOffset? TopicPartitionOffset { get; } = TopicPartitionOffset;
    }
}
