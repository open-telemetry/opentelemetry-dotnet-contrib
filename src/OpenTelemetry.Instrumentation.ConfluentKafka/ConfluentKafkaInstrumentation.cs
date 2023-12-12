// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.ConfluentKafka;

internal class ConfluentKafkaInstrumentation : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly ConfluentKafkaMeterInstrumentation meterInstrumentation;

    public ConfluentKafkaInstrumentation(IServiceProvider serviceProvider, ConfluentKafkaMeterInstrumentation confluentKafkaMeterInstrumentation)
    {
        this.serviceProvider = serviceProvider;
        this.meterInstrumentation = confluentKafkaMeterInstrumentation;

        Task.Run(() => this.meterInstrumentation.ExecuteAsync(default));
    }

    internal ConcurrentDictionary<string, ConfluentKafkaProducerInstrumentation> InstrumentedProducers { get; } = new();

    internal ConcurrentDictionary<string, ConfluentKafkaConsumerInstrumentation> InstrumentedConsumers { get; } = new();

    /// <summary>
    /// Adds an <see cref="InstrumentedProducer{TKey, TValue}"/> to the instrumentation.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="name">Name to use when retrieving options.</param>
    /// <param name="producerBuilder"><see cref="InstrumentedProducerBuilder{TKey, TValue}"/>.</param>
    public void AddProducer<TKey, TValue>(
        string name,
        InstrumentedProducerBuilder<TKey, TValue> producerBuilder)
    {
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(producerBuilder);

        this.InstrumentedProducers.GetOrAdd(name, _ =>
        {
            var channel = this.serviceProvider.GetRequiredService<MetricsChannel>();
            return new ConfluentKafkaProducerInstrumentation<TKey, TValue>(producerBuilder, channel);
        });
    }

    /// <summary>
    /// Adds an <see cref="InstrumentedProducer{TKey, TValue}"/> to the instrumentation.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="name">Name to use when retrieving options.</param>
    /// <param name="consumerBuilder"><see cref="InstrumentedConsumerBuilder{TKey, TValue}"/>.</param>
    public void AddConsumer<TKey, TValue>(
        string name,
        InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder)
    {
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(consumerBuilder);

        this.InstrumentedConsumers.GetOrAdd(name, _ =>
        {
            var channel = this.serviceProvider.GetRequiredService<MetricsChannel>();
            return new ConfluentKafkaConsumerInstrumentation<TKey, TValue>(consumerBuilder, channel);
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var instrumentation in this.InstrumentedProducers.Values)
        {
            instrumentation.Dispose();
        }

        this.InstrumentedProducers.Clear();

        foreach (var instrumentation in this.InstrumentedConsumers.Values)
        {
            instrumentation.Dispose();
        }

        this.InstrumentedConsumers.Clear();
    }
}
