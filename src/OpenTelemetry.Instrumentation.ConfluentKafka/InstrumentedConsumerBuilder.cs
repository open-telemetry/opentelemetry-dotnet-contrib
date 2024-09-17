// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Instrumentation.ConfluentKafka;

namespace Confluent.Kafka;

/// <summary>
/// A builder of <see cref="IConsumer{TKey, TValue}"/> with support for instrumentation.
/// </summary>
/// <typeparam name="TKey">Type of the key.</typeparam>
/// <typeparam name="TValue">Type of value.</typeparam>
public sealed class InstrumentedConsumerBuilder<TKey, TValue> : ConsumerBuilder<TKey, TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedConsumerBuilder{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="config"> A collection of librdkafka configuration parameters (refer to https://github.com/edenhill/librdkafka/blob/master/CONFIGURATION.md) and parameters specific to this client (refer to: <see cref="ConfigPropertyNames" />). At a minimum, 'bootstrap.servers' must be specified.</param>
    public InstrumentedConsumerBuilder(IEnumerable<KeyValuePair<string, string>> config)
        : base(config)
    {
    }

    internal ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>? Options { get; set; }

    /// <summary>
    /// Build a new IConsumer instance.
    /// </summary>
    /// <returns>an <see cref="IProducer{TKey,TValue}"/>.</returns>
    public override IConsumer<TKey, TValue> Build()
    {
        Debug.Assert(this.Options != null, "Options should not be null.");

        ConsumerConfig config = (ConsumerConfig)this.Config;

        var consumer = new InstrumentedConsumer<TKey, TValue>(base.Build(), this.Options!);
        consumer.GroupId = config.GroupId;

        return consumer;
    }
}
