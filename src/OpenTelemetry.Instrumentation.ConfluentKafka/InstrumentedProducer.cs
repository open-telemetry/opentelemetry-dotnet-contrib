// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal sealed class InstrumentedProducer<TKey, TValue> : IProducer<TKey, TValue>
{
    private readonly IProducer<TKey, TValue> producerImplementation;
    private readonly TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;
    private readonly ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> options;

    public InstrumentedProducer(IProducer<TKey, TValue> producer, ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> options)
    {
        this.producerImplementation = producer;
        this.options = options;
    }

    public Handle Handle => this.producerImplementation.Handle;

    public string Name => this.producerImplementation.Name;

    public int AddBrokers(string brokers)
    {
        return this.producerImplementation.AddBrokers(brokers);
    }

    public void SetSaslCredentials(string username, string password)
    {
        this.producerImplementation.SetSaslCredentials(username, password);
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        Message<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (this.options.Traces)
        {
            using Activity? activity = this.StartActivity("publish", topic) ?? Activity.Current;
            this.InjectActivity(activity, message);
        }

        return await this.producerImplementation.ProduceAsync(topic, message, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        TopicPartition topicPartition,
        Message<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        if (this.options.Traces)
        {
            using Activity? activity = this.StartActivity("publish", topicPartition) ?? Activity.Current;
            this.InjectActivity(activity, message);
        }

        return await this.producerImplementation.ProduceAsync(topicPartition, message, cancellationToken).ConfigureAwait(false);
    }

    public void Produce(string topic, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
    {
        if (this.options.Traces)
        {
            using Activity? activity = this.StartActivity("publish", topic) ?? Activity.Current;
            this.InjectActivity(activity, message);
        }

        this.producerImplementation.Produce(topic, message, deliveryHandler);
    }

    public void Produce(TopicPartition topicPartition, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
    {
        if (this.options.Traces)
        {
            using Activity? activity = this.StartActivity("publish", topicPartition) ?? Activity.Current;
            this.InjectActivity(activity, message);
        }

        this.producerImplementation.Produce(topicPartition, message, deliveryHandler);
    }

    public int Poll(TimeSpan timeout)
    {
        return this.producerImplementation.Poll(timeout);
    }

    public int Flush(TimeSpan timeout)
    {
        return this.producerImplementation.Flush(timeout);
    }

    public void Flush(CancellationToken cancellationToken = default)
    {
        this.producerImplementation.Flush(cancellationToken);
    }

    public void InitTransactions(TimeSpan timeout)
    {
        this.producerImplementation.InitTransactions(timeout);
    }

    public void BeginTransaction()
    {
        this.producerImplementation.BeginTransaction();
    }

    public void CommitTransaction(TimeSpan timeout)
    {
        this.producerImplementation.CommitTransaction(timeout);
    }

    public void CommitTransaction()
    {
        this.producerImplementation.CommitTransaction();
    }

    public void AbortTransaction(TimeSpan timeout)
    {
        this.producerImplementation.AbortTransaction(timeout);
    }

    public void AbortTransaction()
    {
        this.producerImplementation.AbortTransaction();
    }

    public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
    {
        this.producerImplementation.SendOffsetsToTransaction(offsets, groupMetadata, timeout);
    }

    public void Dispose()
    {
        this.producerImplementation.Dispose();
    }

    private Activity? StartActivity(string operation, string topic)
    {
        var activity = ConfluentKafkaCommon.ActivitySource.StartActivity(string.Concat(topic, " ", operation), ActivityKind.Producer);
        if (activity == null)
        {
            return null;
        }

        activity.SetTag(SemanticConventions.AttributeMessagingSystem, "kafka");
        activity.SetTag("messaging.client_id", this.Name);
        activity.SetTag("messaging.destination.name", topic);
        activity.SetTag(SemanticConventions.AttributeMessagingOperation, operation);
        return activity;
    }

    private Activity? StartActivity(string operation, TopicPartition topicPartition)
    {
        var activity = this.StartActivity(operation, topicPartition.Topic);
        if (activity == null)
        {
            return null;
        }

        activity.SetTag("messaging.kafka.destination.partition", topicPartition.Partition.Value);
        return activity;
    }

    private void InjectActivity(Activity? activity, Message<TKey, TValue> message)
    {
        (ActivityContext activityContext, Baggage baggage) = activity != null
            ? (activity.Context, Baggage.Current.SetBaggage(activity.Baggage))
            : (default, default);
        this.propagator.Inject(new PropagationContext(activityContext, baggage), message, this.InjectTraceContext);
    }

    private void InjectTraceContext(Message<TKey, TValue> message, string key, string value)
    {
        message.Headers ??= new Headers();
        message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
}
