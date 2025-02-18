// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// Consumes a message and creates <see href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#span-kind">a process span</see> embracing the <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.
    /// </summary>
    /// <param name="consumer">The <see cref="IConsumer{TKey,TValue}"/>.</param>
    /// <param name="handler">A <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.</param>
    /// <param name="startActivityAsLink">true if you want the <see cref="System.Diagnostics.Activity" /> to be started as a Link rather than a child of the message's parent</param>
    /// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <returns>A <see cref="ValueTask"/>.</returns>
    public static ValueTask<ConsumeResult<TKey, TValue>?> ConsumeAndProcessMessageAsync<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        OpenTelemetryConsumeAndProcessMessageHandler<TKey, TValue> handler,
        bool startActivityAsLink = false) =>
        ConsumeAndProcessMessageAsync(consumer, handler, CancellationToken.None, startActivityAsLink);

    /// <summary>
    /// Consumes a message and creates <see href="https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#span-kind">a process span</see> embracing the <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.
    /// </summary>
    /// <param name="consumer">The <see cref="IConsumer{TKey,TValue}"/>.</param>
    /// <param name="handler">A <see cref="OpenTelemetryConsumeAndProcessMessageHandler{TKey,TValue}"/>.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/>.</param>
    /// <param name="startActivityAsLink">true if you want the <see cref="System.Diagnostics.Activity" /> to be started as a Link rather than a child of the message's parent</param>
    /// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <returns>A <see cref="ValueTask"/>.</returns>
    public static async ValueTask<ConsumeResult<TKey, TValue>?> ConsumeAndProcessMessageAsync<TKey, TValue>(
        this IConsumer<TKey, TValue> consumer,
        OpenTelemetryConsumeAndProcessMessageHandler<TKey, TValue> handler,
        CancellationToken cancellationToken,
        bool startActivityAsLink = false)
    {
#if NET
        ArgumentNullException.ThrowIfNull(consumer);
#else
        if (consumer == null)
        {
            throw new ArgumentNullException(nameof(consumer));
        }
#endif

#if NET
        ArgumentNullException.ThrowIfNull(handler);
#else
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }
#endif

        var consumeResult = consumer.Consume(cancellationToken);

        if (consumeResult?.Message == null || consumeResult.IsPartitionEOF)
        {
            return consumeResult;
        }

        var processActivity = consumeResult.StartProcessActivity(consumer, startActivityAsLink);

        try
        {
            await handler(consumeResult, processActivity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            processActivity?.SetStatus(ActivityStatusCode.Error);
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

    /// <summary>
    /// Start a <see cref="System.Diagnostics.Activity" /> for the Processing of the message
    /// </summary>
    /// <param name="consumeResult">The <see cref="ConsumeResult{TKey,TValue}" /> to start the Activity for</param>
    /// <param name="consumer">The consumer that initiated the Consume operation</param>
    /// <param name="startAsLinkedActivity">true if you want the <see cref="System.Diagnostics.Activity" /> to be started as a Link rather than a child of the message's parent</param>
    /// <typeparam name="TKey">The type of key of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of value of the <see cref="ConsumeResult{TKey,TValue}"/>.</typeparam>
    /// <returns>A started <see cref="System.Diagnostics.Activity" /></returns>
    /// <exception cref="ArgumentNullException">Throws an exception if the consumeResult or the Consumer are null</exception>
    public static Activity? StartProcessActivity<TKey, TValue>(
        this ConsumeResult<TKey, TValue> consumeResult,
        IConsumer<TKey, TValue> consumer,
        bool startAsLinkedActivity = false)
    {
        if (consumeResult == null)
        {
            throw new ArgumentNullException(nameof(consumeResult));
        }

        if (consumer == null)
        {
            throw new ArgumentNullException(nameof(consumer));
        }

        var spanName = $"{ConfluentKafkaCommon.ProcessOperationName} {consumeResult.Topic}";

        var propagationContext = ExtractPropagationContext(consumeResult.Message.Headers);

        var links = new List<ActivityLink>();
        if (startAsLinkedActivity)
        {
            links.Add(new ActivityLink(propagationContext.ActivityContext));
        }

        var tags = new TagList
        {
            { SemanticConventions.AttributeMessagingSystem, ConfluentKafkaCommon.KafkaMessagingSystem },
            { SemanticConventions.AttributeMessagingClientId, consumer.Name },
            { SemanticConventions.AttributeMessagingDestinationName, consumeResult.Topic },
            { SemanticConventions.AttributeMessagingKafkaDestinationPartition, consumeResult.TopicPartition.Partition.Value },
            { SemanticConventions.AttributeMessagingKafkaMessageOffset, consumeResult.Offset.Value },
            { SemanticConventions.AttributeMessagingOperation, ConfluentKafkaCommon.ProcessOperationName },
        };

        if (consumer is InstrumentedConsumer<TKey, TValue> instrumentedConsumer)
        {
            tags.Add(SemanticConventions.AttributeMessagingKafkaConsumerGroup, instrumentedConsumer.Config.GroupId);
        }

        var parentContext = startAsLinkedActivity ? default : propagationContext.ActivityContext;

        var activity = ConfluentKafkaCommon.ProcessorActivitySource.StartActivity(
            spanName,
            ActivityKind.Consumer,
            parentContext,
            tags: tags,
            links: links);

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
