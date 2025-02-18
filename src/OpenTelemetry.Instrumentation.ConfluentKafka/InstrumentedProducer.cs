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
    private readonly TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;
    private readonly IProducer<TKey, TValue> producer;

    public InstrumentedProducer(
        IProducer<TKey, TValue> producer)
    {
        this.producer = producer;
    }

    public Handle Handle => this.producer.Handle;

    public string Name => this.producer.Name;

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
        var start = DateTimeOffset.UtcNow;
        using var activity = this.StartPublishActivity(topic, message);
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
            activity?.AddException(
                produceException.InnerException,
                tags: new TagList()
                {
                    { SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException) },
                });
            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(
                argumentException,
                tags: new TagList()
                {
                    { SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException) },
                });

            throw;
        }
        finally
        {
            var end = DateTimeOffset.UtcNow;
            activity?.SetEndTime(end.UtcDateTime);
            var duration = end - start;

            RecordPublish(topic, duration, errorType);
        }

        return result;
    }

    public async Task<DeliveryResult<TKey, TValue>> ProduceAsync(
        TopicPartition topicPartition,
        Message<TKey, TValue> message,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.UtcNow;
        using var activity = this.StartPublishActivity(topicPartition.Topic, message, topicPartition.Partition);
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
            activity?.AddException(
                produceException.InnerException,
                tags: new TagList()
                {
                    { SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException) },
                });
            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);

            activity?.AddException(
                argumentException,
                tags: new TagList()
                {
                    { SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException) },
                });
            throw;
        }
        finally
        {
            var end = DateTimeOffset.UtcNow;
            activity?.SetEndTime(end.UtcDateTime);
            var duration = end - start;

            RecordPublish(topicPartition, duration, errorType);
        }

        return result;
    }

    public void Produce(string topic, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
    {
        var start = DateTimeOffset.UtcNow;
        using var activity = this.StartPublishActivity(topic, message);
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
            activity?.AddException(
                produceException.InnerException,
                tags: new TagList()
                {
                    { SemanticConventions.AttributeErrorType, errorType = FormatProduceException(produceException) },
                });

            throw;
        }
        catch (ArgumentException argumentException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(
                argumentException,
                tags: new TagList()
                {
                    { SemanticConventions.AttributeErrorType, errorType = FormatArgumentException(argumentException) },
                });

            throw;
        }
        finally
        {
            var end = DateTimeOffset.UtcNow;
            var duration = end - start;

            RecordPublish(topic, duration, errorType);
        }
    }

    public void Produce(TopicPartition topicPartition, Message<TKey, TValue> message, Action<DeliveryReport<TKey, TValue>>? deliveryHandler = null)
    {
        var start = DateTimeOffset.UtcNow;
        using var activity = this.StartPublishActivity(topicPartition.Topic, message, topicPartition.Partition);
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
            var end = DateTimeOffset.UtcNow;
            var duration = end - start;

            RecordPublish(topicPartition, duration, errorType);
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
        this.producer.Dispose();
    }

    private static string FormatProduceException(ProduceException<TKey, TValue> produceException) =>
        $"ProduceException: {produceException.Error.Code}";

    private static string FormatArgumentException(ArgumentException argumentException) =>
        $"ArgumentException: {argumentException.ParamName}";

    private static void GetTags(string topic, out TagList tags, int? partition = null, string? errorType = null)
    {
        tags = new TagList()
        {
            new KeyValuePair<string, object?>(
                SemanticConventions.AttributeMessagingOperation,
                ConfluentKafkaCommon.PublishOperationName),
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

    private static void RecordPublish(string topic, TimeSpan duration, string? errorType = null)
    {
        if (!ConfluentKafkaCommon.PublishMessagesCounter.Enabled &&
            ConfluentKafkaCommon.PublishDurationHistogram.Enabled)
        {
            return;
        }

        GetTags(topic, out var tags, partition: null, errorType);

        ConfluentKafkaCommon.PublishMessagesCounter.Add(1, in tags);
        ConfluentKafkaCommon.PublishDurationHistogram.Record(duration.TotalSeconds, in tags);
    }

    private static void RecordPublish(TopicPartition topicPartition, TimeSpan duration, string? errorType = null)
        => RecordPublish(topicPartition.Topic, duration, errorType);

    private Activity? StartPublishActivity(string topic, Message<TKey, TValue> message, int? partition = null)
    {
        if (!ConfluentKafkaCommon.ProducerActivitySource.HasListeners())
        {
            return null;
        }

        var spanName = $"{ConfluentKafkaCommon.PublishOperationName} {topic}";

        var tags = new TagList
        {
            { SemanticConventions.AttributeMessagingSystem, ConfluentKafkaCommon.KafkaMessagingSystem },
            { SemanticConventions.AttributeMessagingClientId, this.Name },
            { SemanticConventions.AttributeMessagingDestinationName, topic },
            { SemanticConventions.AttributeMessagingOperation, ConfluentKafkaCommon.PublishOperationName },
        };

        if (message.Key != null)
        {
            tags.Add(SemanticConventions.AttributeMessagingKafkaMessageKey, message.Key);
        }

        if (partition is not null)
        {
            tags.Add(SemanticConventions.AttributeMessagingKafkaDestinationPartition, partition);
        }

        var activity = ConfluentKafkaCommon.ProducerActivitySource.StartActivity(spanName, ActivityKind.Producer, default(ActivityContext), tags: tags);

        return activity;
    }

    private void InjectActivity(Activity? activity, Message<TKey, TValue> message)
    {
        this.propagator.Inject(new PropagationContext(activity?.Context ?? default, Baggage.Current), message, this.InjectTraceContext);
    }

    private void InjectTraceContext(Message<TKey, TValue> message, string key, string value)
    {
        message.Headers ??= [];
        message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
    }
}
