// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static partial class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder)
        => AddKafkaProducerInstrumentation<TKey, TValue>(builder, name: null, producerBuilder: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="producerBuilder"><see cref="InstrumentedProducerBuilder{TKey,TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        InstrumentedProducerBuilder<TKey, TValue> producerBuilder)
    {
        Guard.ThrowIfNull(producerBuilder);

        return AddKafkaProducerInstrumentation(builder, name: null, producerBuilder);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="producerBuilder">Optional <see cref="InstrumentedProducerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        string? name,
        InstrumentedProducerBuilder<TKey, TValue>? producerBuilder)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            services.Configure<ConfluentKafkaProducerInstrumentationOptions<TKey, TValue>>(name, EnableTracing);
        });

        return builder
            .AddSource(ConfluentKafkaCommon.InstrumentationName)
            .AddInstrumentation(sp =>
            {
                if (producerBuilder == null)
                {
                    producerBuilder = sp.GetRequiredService<InstrumentedProducerBuilder<TKey, TValue>>();
                    var options = sp.GetRequiredService<IOptionsMonitor<ConfluentKafkaProducerInstrumentationOptions<TKey, TValue>>>();
                    producerBuilder.Options = options.Get(name);
                }

                if (producerBuilder.Options == null)
                {
                    producerBuilder.Options = new ConfluentKafkaProducerInstrumentationOptions<TKey, TValue>();
                    EnableTracing(producerBuilder.Options);
                }

                return new ConfluentKafkaProducerInstrumentation<TKey, TValue>(producerBuilder);
            });
    }

    private static void EnableTracing<TKey, TValue>(ConfluentKafkaProducerInstrumentationOptions<TKey, TValue> options) =>
        options.Traces = true;
}
