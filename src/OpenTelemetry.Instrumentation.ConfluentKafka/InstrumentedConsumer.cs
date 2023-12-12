// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class InstrumentedConsumer<TKey, TValue> : IConsumer<TKey, TValue>
{
    private const string ReceiveOperationName = "receive";
    private const string KafkaMessagingSystem = "kafka";
    private readonly ConsumerMeterInstrumentation consumerMeterInstrumentation = new();
    private readonly IConsumer<TKey, TValue> consumer;
    private readonly ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue> options;

    public InstrumentedConsumer(IConsumer<TKey, TValue> consumer, ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue> options)
    {
        this.consumer = consumer;
        this.options = options;
    }

    public Handle Handle => this.consumer.Handle;

    public string Name => this.consumer.Name;

    public string MemberId => this.consumer.MemberId;

    public List<TopicPartition> Assignment => this.consumer.Assignment;

    public List<string> Subscription => this.consumer.Subscription;

    public IConsumerGroupMetadata ConsumerGroupMetadata => this.consumer.ConsumerGroupMetadata;

    public string? GroupId { get; internal set; }

    public void Dispose()
    {
        this.consumerMeterInstrumentation.Dispose();
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

    public ConsumeResult<TKey, TValue>? Consume(int millisecondsTimeout)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        try
        {
            var result = this.consumer.Consume(millisecondsTimeout);
            if (result is { IsPartitionEOF: false })
            {
                this.InstrumentConsumption(start, result);
            }

            return result;
        }
        catch (ConsumeException e)
        {
            this.InstrumentConsumptionError(start, e);
            throw;
        }
    }

    public ConsumeResult<TKey, TValue>? Consume(CancellationToken cancellationToken = default)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;

        try
        {
            var result = this.consumer.Consume(cancellationToken);
            if (result is { IsPartitionEOF: false })
            {
                this.InstrumentConsumption(start, result);
            }

            return result;
        }
        catch (ConsumeException e)
        {
            this.InstrumentConsumptionError(start, e);
            throw;
        }
    }

    public ConsumeResult<TKey, TValue>? Consume(TimeSpan timeout)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;

        try
        {
            var result = this.consumer.Consume(timeout);
            if (result is { IsPartitionEOF: false })
            {
                this.InstrumentConsumption(start, result);
            }

            return result;
        }
        catch (ConsumeException e)
        {
            this.InstrumentConsumptionError(start, e);
            throw;
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

    private static PropagationContext ExtractPropagationContext(Headers? headers)
        => Propagators.DefaultTextMapPropagator.Extract(default, headers, ExtractTraceContext);

    private static IEnumerable<string> ExtractTraceContext(Headers? headers, string value)
    {
        if (headers?.TryGetLastBytes(value, out var bytes) == true)
        {
            yield return Encoding.UTF8.GetString(bytes);
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> GetTags(string topic, int? partition = null, string? errorType = null)
    {
        yield return new KeyValuePair<string, object?>(
            SemanticConventions.AttributeMessagingOperation,
            ReceiveOperationName);
        yield return new KeyValuePair<string, object?>(
            SemanticConventions.AttributeMessagingSystem,
            KafkaMessagingSystem);
        yield return new KeyValuePair<string, object?>(
            SemanticConventions.AttributeMessagingDestinationName,
            topic);
        if (partition is not null)
        {
            yield return new KeyValuePair<string, object?>(
                SemanticConventions.AttributeMessagingKafkaDestinationPartition,
                partition);
        }

        if (errorType is not null)
        {
            yield return new KeyValuePair<string, object?>(
                SemanticConventions.AttributeErrorType,
                errorType);
        }
    }

    private void RecordReceive(TopicPartition topicPartition, TimeSpan duration, string? errorType = null)
    {
        var tags = GetTags(topicPartition.Topic, partition: topicPartition.Partition, errorType).ToArray();
        this.consumerMeterInstrumentation.RecordReceivedMessage(tags);
        this.consumerMeterInstrumentation.RecordReceiveDuration(duration.TotalSeconds, tags);
    }

    private void InstrumentConsumption(DateTimeOffset startTime, ConsumeResult<TKey, TValue> result)
    {
        Activity? activity = null;
        if (this.options.Traces)
        {
            ConsumeResult consumeResult = new(result.TopicPartitionOffset, result.Message.Headers, result.Message.Key);
            PropagationContext propagationContext = consumeResult.Headers != null
                ? ExtractPropagationContext(consumeResult.Headers)
                : default;
            var spanName = string.IsNullOrEmpty(consumeResult.TopicPartitionOffset?.Topic)
                ? ReceiveOperationName
                : string.Concat(consumeResult.TopicPartitionOffset!.Topic, " ", ReceiveOperationName);

            activity = this.StartReceiveActivity(spanName, propagationContext, startTime, consumeResult.TopicPartitionOffset, consumeResult.Key);
            activity?.Stop();
        }

        if (this.options.Metrics)
        {
            TimeSpan duration = activity?.Duration ?? DateTimeOffset.UtcNow - startTime;
            this.RecordReceive(result.TopicPartition, duration);
        }

        activity?.Dispose();
    }

    private void InstrumentConsumptionError(DateTimeOffset startTime, ConsumeException exception)
    {
        Activity? activity = null;
        string? errorType = null;
        if (this.options.Traces)
        {
            ConsumeResult consumeResult = new(exception.ConsumerRecord.TopicPartitionOffset, exception.ConsumerRecord.Message.Headers, exception.ConsumerRecord.Message.Key);
            PropagationContext propagationContext = consumeResult.Headers != null
                ? ExtractPropagationContext(consumeResult.Headers)
                : default;
            var spanName = string.IsNullOrEmpty(consumeResult.TopicPartitionOffset?.Topic)
                ? ReceiveOperationName
                : string.Concat(consumeResult.TopicPartitionOffset!.Topic, " ", ReceiveOperationName);

            activity = this.StartReceiveActivity(spanName, propagationContext, startTime, consumeResult.TopicPartitionOffset, consumeResult.Key);
            errorType = FormatConsumeException(exception);
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error);
                if (activity.IsAllDataRequested)
                {
                    activity.SetTag(SemanticConventions.AttributeErrorType, errorType);
                }

                activity.Stop();
            }
        }

        if (this.options.Metrics)
        {
            TimeSpan duration = activity?.Duration ?? DateTimeOffset.UtcNow - startTime;
            errorType ??= FormatConsumeException(exception);
            this.RecordReceive(exception.ConsumerRecord.TopicPartition, duration, errorType);
        }

        activity?.Dispose();
    }

    private Activity? StartReceiveActivity(string spanName, PropagationContext propagationContext, DateTimeOffset start, TopicPartitionOffset? topicPartitionOffset, object? key)
    {
        ActivityLink[] activityLinks = propagationContext.ActivityContext.IsValid()
            ? new[] { new ActivityLink(propagationContext.ActivityContext) }
            : Array.Empty<ActivityLink>();

        Activity? activity = ConfluentKafkaCommon.ActivitySource.StartActivity(spanName, kind: ActivityKind.Consumer, links: activityLinks, startTime: start, parentContext: default);
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(SemanticConventions.AttributeMessagingSystem, KafkaMessagingSystem);
            activity.SetTag(SemanticConventions.AttributeMessagingClientId, this.Name);
            activity.SetTag(SemanticConventions.AttributeMessagingDestinationName, topicPartitionOffset?.Topic);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaDestinationPartition, topicPartitionOffset?.Partition.Value);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaMessageOffset, topicPartitionOffset?.Offset.Value);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaConsumerGroup, this.GroupId);
            activity.SetTag(SemanticConventions.AttributeMessagingOperation, ReceiveOperationName);
            if (key != null)
            {
                activity.SetTag(SemanticConventions.AttributeMessagingKafkaMessageKey, key);
            }
        }

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
