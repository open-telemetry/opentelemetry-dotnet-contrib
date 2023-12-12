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
    private readonly TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;
    private readonly IConsumer<TKey, TValue> consumerImplementation;
    private readonly ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue> options;
    private Activity? previousConsumeActivity;

    public InstrumentedConsumer(IConsumer<TKey, TValue> consumer, ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue> options)
    {
        this.consumerImplementation = consumer;
        this.options = options;
    }

    public Handle Handle => this.consumerImplementation.Handle;

    public string Name => this.consumerImplementation.Name;

    public string MemberId => this.consumerImplementation.MemberId;

    public List<TopicPartition> Assignment => this.consumerImplementation.Assignment;

    public List<string> Subscription => this.consumerImplementation.Subscription;

    public IConsumerGroupMetadata ConsumerGroupMetadata => this.consumerImplementation.ConsumerGroupMetadata;

    public string? GroupId { get; internal set; }

    public void Dispose()
    {
        this.previousConsumeActivity?.Dispose();
        this.consumerImplementation.Dispose();
    }

    public int AddBrokers(string brokers)
    {
        return this.consumerImplementation.AddBrokers(brokers);
    }

    public void SetSaslCredentials(string username, string password)
    {
        this.consumerImplementation.SetSaslCredentials(username, password);
    }

    public ConsumeResult<TKey, TValue>? Consume(int millisecondsTimeout)
    {
        this.EnsurePreviousConsumeActivityIsStopped();

        var consumeResult = this.consumerImplementation.Consume(millisecondsTimeout);
        if (this.options.Traces && consumeResult?.Message != null)
        {
            var propagationContext = this.ExtractActivity(consumeResult.Message);
            Activity.Current = this.previousConsumeActivity = this.StartActivity("receive", consumeResult.TopicPartitionOffset, propagationContext);
        }

        return consumeResult;
    }

    public ConsumeResult<TKey, TValue>? Consume(CancellationToken cancellationToken = default)
    {
        this.EnsurePreviousConsumeActivityIsStopped();

        var consumeResult = this.consumerImplementation.Consume(cancellationToken);
        if (this.options.Traces && consumeResult?.Message != null)
        {
            var propagationContext = this.ExtractActivity(consumeResult.Message);
            Activity.Current = this.previousConsumeActivity = this.StartActivity("receive", consumeResult.TopicPartitionOffset, propagationContext);
        }

        return consumeResult;
    }

    public ConsumeResult<TKey, TValue>? Consume(TimeSpan timeout)
    {
        this.EnsurePreviousConsumeActivityIsStopped();

        var consumeResult = this.consumerImplementation.Consume(timeout);
        if (this.options.Traces && consumeResult?.Message != null)
        {
            var propagationContext = this.ExtractActivity(consumeResult.Message);
            Activity.Current = this.previousConsumeActivity = this.StartActivity("receive", consumeResult.TopicPartitionOffset, propagationContext);
        }

        return consumeResult;
    }

    public void Subscribe(IEnumerable<string> topics)
    {
        this.consumerImplementation.Subscribe(topics);
    }

    public void Subscribe(string topic)
    {
        this.consumerImplementation.Subscribe(topic);
    }

    public void Unsubscribe()
    {
        this.consumerImplementation.Unsubscribe();
    }

    public void Assign(TopicPartition partition)
    {
        this.consumerImplementation.Assign(partition);
    }

    public void Assign(TopicPartitionOffset partition)
    {
        this.consumerImplementation.Assign(partition);
    }

    public void Assign(IEnumerable<TopicPartitionOffset> partitions)
    {
        this.consumerImplementation.Assign(partitions);
    }

    public void Assign(IEnumerable<TopicPartition> partitions)
    {
        this.consumerImplementation.Assign(partitions);
    }

    public void IncrementalAssign(IEnumerable<TopicPartitionOffset> partitions)
    {
        this.consumerImplementation.IncrementalAssign(partitions);
    }

    public void IncrementalAssign(IEnumerable<TopicPartition> partitions)
    {
        this.consumerImplementation.IncrementalAssign(partitions);
    }

    public void IncrementalUnassign(IEnumerable<TopicPartition> partitions)
    {
        this.consumerImplementation.IncrementalUnassign(partitions);
    }

    public void Unassign()
    {
        this.consumerImplementation.Unassign();
    }

    public void StoreOffset(ConsumeResult<TKey, TValue> result)
    {
        this.consumerImplementation.StoreOffset(result);
    }

    public void StoreOffset(TopicPartitionOffset offset)
    {
        this.consumerImplementation.StoreOffset(offset);
    }

    public List<TopicPartitionOffset> Commit()
    {
        return this.consumerImplementation.Commit();
    }

    public void Commit(IEnumerable<TopicPartitionOffset> offsets)
    {
        this.consumerImplementation.Commit(offsets);
    }

    public void Commit(ConsumeResult<TKey, TValue> result)
    {
        this.consumerImplementation.Commit(result);
    }

    public void Seek(TopicPartitionOffset tpo)
    {
        this.consumerImplementation.Seek(tpo);
    }

    public void Pause(IEnumerable<TopicPartition> partitions)
    {
        this.consumerImplementation.Pause(partitions);
    }

    public void Resume(IEnumerable<TopicPartition> partitions)
    {
        this.consumerImplementation.Resume(partitions);
    }

    public List<TopicPartitionOffset> Committed(TimeSpan timeout)
    {
        return this.consumerImplementation.Committed(timeout);
    }

    public List<TopicPartitionOffset> Committed(IEnumerable<TopicPartition> partitions, TimeSpan timeout)
    {
        return this.consumerImplementation.Committed(partitions, timeout);
    }

    public Offset Position(TopicPartition partition)
    {
        return this.consumerImplementation.Position(partition);
    }

    public List<TopicPartitionOffset> OffsetsForTimes(IEnumerable<TopicPartitionTimestamp> timestampsToSearch, TimeSpan timeout)
    {
        return this.consumerImplementation.OffsetsForTimes(timestampsToSearch, timeout);
    }

    public WatermarkOffsets GetWatermarkOffsets(TopicPartition topicPartition)
    {
        return this.consumerImplementation.GetWatermarkOffsets(topicPartition);
    }

    public WatermarkOffsets QueryWatermarkOffsets(TopicPartition topicPartition, TimeSpan timeout)
    {
        return this.consumerImplementation.QueryWatermarkOffsets(topicPartition, timeout);
    }

    public void Close()
    {
        this.consumerImplementation.Close();
    }

    private Activity? StartActivity(string operation, TopicPartitionOffset topicPartitionOffset, PropagationContext propagationContext)
    {
        var activity = ConfluentKafkaCommon.ActivitySource.StartActivity(name: string.Concat(topicPartitionOffset.Topic, " ", operation), kind: ActivityKind.Consumer, propagationContext.ActivityContext);
        if (activity != null)
        {
            activity.SetTag(SemanticConventions.AttributeMessagingSystem, "kafka");
            activity.SetTag("messaging.client_id", this.Name);
            activity.SetTag("messaging.destination.name", topicPartitionOffset.Topic);
            activity.SetTag("messaging.kafka.destination.partition", topicPartitionOffset.Partition.Value);
            activity.SetTag("messaging.kafka.message.offset", topicPartitionOffset.Offset.Value);
            activity.SetTag("messaging.kafka.consumer.group", this.GroupId);
            activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);

            foreach (KeyValuePair<string, string> baggage in propagationContext.Baggage)
            {
                activity.SetBaggage(baggage.Key, baggage.Value);
            }
        }

        return activity;
    }

    private PropagationContext ExtractActivity(Message<TKey, TValue> message)
    {
        PropagationContext propagationContext = Activity.Current != null
            ? new PropagationContext(Activity.Current.Context, Baggage.Current)
            : default;
        return this.propagator.Extract(propagationContext, message, this.ExtractTraceContext);
    }

    private IEnumerable<string> ExtractTraceContext(Message<TKey, TValue> message, string value)
    {
        if (message.Headers?.TryGetLastBytes(value, out var bytes) == true)
        {
            yield return Encoding.UTF8.GetString(bytes);
        }
    }

    private void EnsurePreviousConsumeActivityIsStopped()
    {
        if (this.previousConsumeActivity is { IsStopped: false })
        {
            this.previousConsumeActivity.Stop();
        }
    }
}
