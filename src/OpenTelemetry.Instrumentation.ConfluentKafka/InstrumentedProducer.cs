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
    private const string PublishOperationName = "publish";
    private const string KafkaMessagingSystem = "kafka";

    private readonly TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;
    private readonly ProducerMeterInstrumentation producerMeterInstrumentation = new();
    private readonly IProducer<TKey, TValue> producer;
    private readonly ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> options;

    public InstrumentedProducer(
        IProducer<TKey, TValue> producer,
        ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> options)
    {
        this.producer = producer;
        this.options = options;
    }

    public Handle Handle => this.producer.Handle;

    public string Name => this.producer.Name;

    internal ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> Options => this.options;

    public int AddBrokers(string brokers)
    {
        return this.producer.AddBrokers(brokers);
    }

    public void SetSaslCredentials(string username, string password)
    {
        this.producer.SetSaslCredentials(username, password);
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        string topic,
        Message<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        using Activity? activity = this.StartPublishActivity(start, topic, message);
        if (activity != null)
        {
            this.InjectActivity(activity, message);
        }

        DeliveryResult<TKey, TValue> result;
        string? errorType = null;
        try
        {
            result = await this.producer.ProduceAsync(topic, message, cancellationToken).ConfigureAwait(false);
        }
        catch (ProduceException<TKey, TValue> produceException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException));

            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException));

            throw;
        }
        finally
        {
            DateTimeOffset end = DateTimeOffset.UtcNow;
            activity?.SetEndTime(end.UtcDateTime);
            TimeSpan duration = end - start;

            if (this.options.Metrics)
            {
                this.RecordPublish(topic, duration, errorType);
            }
        }

        return result;
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        TopicPartition topicPartition,
        Message<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        using Activity? activity = this.StartPublishActivity(start, topicPartition.Topic, message, topicPartition.Partition);
        if (activity != null)
        {
            this.InjectActivity(activity, message);
        }

        DeliveryResult<TKey, TValue> result;
        string? errorType = null;
        try
        {
            result = await this.producer.ProduceAsync(topicPartition, message, cancellationToken).ConfigureAwait(false);
        }
        catch (ProduceException<TKey, TValue> produceException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException));

            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException));

            throw;
        }
        finally
        {
            DateTimeOffset end = DateTimeOffset.UtcNow;
            activity?.SetEndTime(end.UtcDateTime);
            TimeSpan duration = end - start;

            if (this.options.Metrics)
            {
                this.RecordPublish(topicPartition, duration, errorType);
            }
        }

        return result;
    }

    public void Produce(string topic, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        using Activity? activity = this.StartPublishActivity(start, topic, message);
        if (activity != null)
        {
            this.InjectActivity(activity, message);
        }

        string? errorType = null;
        try
        {
            this.producer.Produce(topic, message, deliveryHandler);
        }
        catch (ProduceException<TKey, TValue> produceException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException));

            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException));

            throw;
        }
        finally
        {
            DateTimeOffset end = DateTimeOffset.UtcNow;
            activity?.SetEndTime(end.UtcDateTime);
            TimeSpan duration = end - start;

            if (this.options.Metrics)
            {
                this.RecordPublish(topic, duration, errorType);
            }
        }
    }

    public void Produce(TopicPartition topicPartition, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        using Activity? activity = this.StartPublishActivity(start, topicPartition.Topic, message, topicPartition.Partition);
        if (activity != null)
        {
            this.InjectActivity(activity, message);
        }

        string? errorType = null;
        try
        {
            this.producer.Produce(topicPartition, message, deliveryHandler);
        }
        catch (ProduceException<TKey, TValue> produceException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException));

            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag(SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException));

            throw;
        }
        finally
        {
            DateTimeOffset end = DateTimeOffset.UtcNow;
            activity?.SetEndTime(end.UtcDateTime);
            TimeSpan duration = end - start;

            if (this.options.Metrics)
            {
                this.RecordPublish(topicPartition, duration, errorType);
            }
        }
    }

    public int Poll(TimeSpan timeout)
    {
        return this.producer.Poll(timeout);
    }

    public int Flush(TimeSpan timeout)
    {
        return this.producer.Flush(timeout);
    }

    public void Flush(CancellationToken cancellationToken = default)
    {
        this.producer.Flush(cancellationToken);
    }

    public void InitTransactions(TimeSpan timeout)
    {
        this.producer.InitTransactions(timeout);
    }

    public void BeginTransaction()
    {
        this.producer.BeginTransaction();
    }

    public void CommitTransaction(TimeSpan timeout)
    {
        this.producer.CommitTransaction(timeout);
    }

    public void CommitTransaction()
    {
        this.producer.CommitTransaction();
    }

    public void AbortTransaction(TimeSpan timeout)
    {
        this.producer.AbortTransaction(timeout);
    }

    public void AbortTransaction()
    {
        this.producer.AbortTransaction();
    }

    public void SendOffsetsToTransaction(IEnumerable<TopicPartitionOffset> offsets, IConsumerGroupMetadata groupMetadata, TimeSpan timeout)
    {
        this.producer.SendOffsetsToTransaction(offsets, groupMetadata, timeout);
    }

    public void Dispose()
    {
        this.producerMeterInstrumentation.Dispose();
        this.producer.Dispose();
    }

    private static string FormatProduceException(ProduceException<TKey, TValue> produceException) =>
        $"ProduceException: {produceException.Error.Code}";

    private static string FormatArgumentException(ArgumentException argumentException) =>
        $"ArgumentException: {argumentException.ParamName}";

    private static IEnumerable<KeyValuePair<string, object?>> GetTags(string topic, int? partition = null, string? errorType = null)
    {
        yield return new KeyValuePair<string, object?>(
            SemanticConventions.AttributeMessagingOperation,
            PublishOperationName);
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

    private void RecordPublish(string topic, TimeSpan duration, string? errorType = null)
    {
        var tags = GetTags(topic, partition: null, errorType).ToArray();
        this.producerMeterInstrumentation.RecordPublishMessage(tags);
        this.producerMeterInstrumentation.RecordPublishDuration(duration.TotalSeconds, tags);
    }

    private void RecordPublish(TopicPartition topicPartition, TimeSpan duration, string? errorType = null)
    {
        var tags = GetTags(topicPartition.Topic, partition: topicPartition.Partition, errorType).ToArray();
        this.producerMeterInstrumentation.RecordPublishMessage(tags);
        this.producerMeterInstrumentation.RecordPublishDuration(duration.TotalSeconds, tags);
    }

    private Activity? StartPublishActivity(DateTimeOffset start, string topic, Message<TKey, TValue> message, int? partition = null)
    {
        var spanName = string.Concat(topic, " ", PublishOperationName);
        var activity = ConfluentKafkaCommon.ActivitySource.StartActivity(name: spanName, kind: ActivityKind.Producer, startTime: start);
        if (activity == null)
        {
            return null;
        }

        if (activity.IsAllDataRequested)
        {
            activity.SetTag(SemanticConventions.AttributeMessagingSystem, KafkaMessagingSystem);
            activity.SetTag(SemanticConventions.AttributeMessagingClientId, this.Name);
            activity.SetTag(SemanticConventions.AttributeMessagingDestinationName, topic);
            activity.SetTag(SemanticConventions.AttributeMessagingOperation, PublishOperationName);

            if (message.Key != null)
            {
                activity.SetTag(SemanticConventions.AttributeMessagingKafkaMessageKey, message.Key);
            }

            if (partition is not null)
            {
                activity.SetTag(SemanticConventions.AttributeMessagingKafkaDestinationPartition, partition);
            }
        }

        return activity;
    }

    private void InjectActivity(Activity? activity, Message<TKey, TValue> message)
    {
        this.propagator.Inject(new PropagationContext(activity?.Context ?? default, Baggage.Current), message, this.InjectTraceContext);
    }

    private void InjectTraceContext(Message<TKey, TValue> message, string key, string value)
    {
        message.Headers ??= new Headers();
        message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
}
