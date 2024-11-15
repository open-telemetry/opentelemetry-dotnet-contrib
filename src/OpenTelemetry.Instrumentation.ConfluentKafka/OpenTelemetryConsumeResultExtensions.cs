// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Trace;

namespace Confluent.Kafka;

/// <summary>
/// <see cref="IConsumer{TKey,TValue}"/> extension methods.
/// </summary>
public static class OpenTelemetryConsumeResultExtensions
{
    /// <summary>
    /// Attempts to extract a <see cref="PropagationContext"/> from the <see cref="ConsumeResult{TKey,TValue}"/>'s <see cref="Headers"/> property.
    /// </summary>
    /// <param name="consumeResult">The <see cref="ConsumeResult{TKey,TValue}"/>.</param>
    /// <param name="propagationContext">The <see cref="PropagationContext"/>.</param>
    /// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <returns>True when a <see cref="PropagationContext"/> has been extracted from <see cref="Headers"/>, otherwise false.</returns>
    public static bool TryExtractPropagationContext<TKey, TValue>(
        this ConsumeResult<TKey, TValue> consumeResult,
        out PropagationContext propagationContext)
    {
#if NET
        ArgumentNullException.ThrowIfNull(consumeResult);
#else
        if (consumeResult == null)
        {
            throw new ArgumentNullException(nameof(consumeResult));
        }
#endif

        try
        {
            propagationContext = ExtractPropagationContext(consumeResult.Message?.Headers);
            return true;
        }
        catch
        {
            propagationContext = default;
            return false;
        }
    }

    /// <summary>
    /// Consumes a message and creates <see href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#span-kind">a process span</see> embracing the <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.
    /// </summary>
    /// <param name="consumer">The <see cref="IConsumer{TKey,TValue}"/>.</param>
    /// <param name="handler">A <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.</param>
    /// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <returns>A <see cref="ValueTask"/>.</returns>
    public static ValueTask<ConsumeResult<TKey, TValue>?> ConsumeAndProcessMessageAsync<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        OpenTelemetryConsumeAndProcessMessageHandler<TKey, TValue> handler) =>
        ConsumeAndProcessMessageAsync(consumer, handler, default);

    /// <summary>
    /// Consumes a message and creates <see href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#span-kind">a process span</see> embracing the <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.
    /// </summary>
    /// <param name="consumer">The <see cref="IConsumer{TKey,TValue}"/>.</param>
    /// <param name="handler">A <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/>.</param>
    /// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <returns>A <see cref="ValueTask"/>.</returns>
    public static async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeAndProcessMessageAsync<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        OpenTelemetryConsumeAndProcessMessageHandler<TKey, TValue> handler,
        CancellationToken cancellationToken)
    {
#if NET
        ArgumentNullException.ThrowIfNull(consumer);
#else
        if (consumer == null)
        {
            throw new ArgumentNullException(nameof(consumer));
        }
#endif

        if (consumer is not InstrumentedConsumer<TKey, TValue> instrumentedConsumer)
        {
            throw new ArgumentException("Invalid consumer type.", nameof(consumer));
        }

#if NET
        ArgumentNullException.ThrowIfNull(handler);
#else
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }
#endif

        var consumeResult = instrumentedConsumer.Consume(cancellationToken);

        if (consumeResult?.Message == null || consumeResult.IsPartitionEOF)
        {
            return consumeResult;
        }

        var processActivity = StartProcessActivity(TryExtractPropagationContext(consumeResult, out var propagationContext) ? propagationContext : default, consumeResult.TopicPartitionOffset, consumeResult.Message.Key, instrumentedConsumer.Name, instrumentedConsumer.GroupId!);

        try
        {
            await handler(consumeResult, processActivity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            processActivity?.SetStatus(Status.Error);
#pragma warning restore CS0618 // Type or member is obsolete
            processActivity?.SetTag(SemanticConventions.AttributeErrorType, ex.GetType().FullName);
        }
        finally
        {
            processActivity?.Dispose();
        }

        return consumeResult;
    }

    internal static PropagationContext ExtractPropagationContext(Headers? headers)
        => Propagators.DefaultTextMapPropagator.Extract(default, headers, ExtractTraceContext);

    private static Activity? StartProcessActivity<TKey>(PropagationContext propagationContext, TopicPartitionOffset? topicPartitionOffset, TKey? key, string clientId, string groupId)
    {
        var spanName = string.IsNullOrEmpty(topicPartitionOffset?.Topic)
            ? ConfluentKafkaCommon.ProcessOperationName
            : string.Concat(topicPartitionOffset!.Topic, " ", ConfluentKafkaCommon.ProcessOperationName);

        ActivityLink[] activityLinks = propagationContext != default && propagationContext.ActivityContext.IsValid()
            ? [new ActivityLink(propagationContext.ActivityContext)]
            : [];

        var activity = ConfluentKafkaCommon.ActivitySource.StartActivity(spanName, kind: ActivityKind.Consumer, links: activityLinks, parentContext: default);
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag(SemanticConventions.AttributeMessagingSystem, ConfluentKafkaCommon.KafkaMessagingSystem);
            activity.SetTag(SemanticConventions.AttributeMessagingClientId, clientId);
            activity.SetTag(SemanticConventions.AttributeMessagingDestinationName, topicPartitionOffset?.Topic);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaDestinationPartition, topicPartitionOffset?.Partition.Value);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaMessageOffset, topicPartitionOffset?.Offset.Value);
            activity.SetTag(SemanticConventions.AttributeMessagingKafkaConsumerGroup, groupId);
            activity.SetTag(SemanticConventions.AttributeMessagingOperation, ConfluentKafkaCommon.ProcessOperationName);
            if (key != null)
            {
                activity.SetTag(SemanticConventions.AttributeMessagingKafkaMessageKey, key);
            }
        }

        return activity;
    }

    private static IEnumerable<string> ExtractTraceContext(Headers? headers, string value)
    {
        if (headers?.TryGetLastBytes(value, out var bytes) == true)
        {
            yield return Encoding.UTF8.GetString(bytes);
        }
    }
}
