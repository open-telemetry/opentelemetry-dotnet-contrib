// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
        => AddKafkaProducerInstrumentation<TKey, TValue>(builder, name: null, producerBuilder: null, configure: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="producerBuilder"><see cref="InstrumentedProducerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        InstrumentedProducerBuilder<TKey, TValue> producerBuilder)
    {
        Guard.ThrowIfNull(producerBuilder);

        return AddKafkaProducerInstrumentation(builder, name: null, producerBuilder, configure: null);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        Action<ConfluentKafkaInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        return AddKafkaProducerInstrumentation<TKey, TValue>(builder, name: null, producerBuilder: null, configure);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="producerBuilder"><see cref="InstrumentedProducerBuilder{TKey,TValue}"/> to instrument.</param>
    /// <param name="configure">Callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        InstrumentedProducerBuilder<TKey, TValue> producerBuilder,
        Action<ConfluentKafkaInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(producerBuilder);
        Guard.ThrowIfNull(configure);

        return AddKafkaProducerInstrumentation(builder, name: null, producerBuilder, configure);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="producerBuilder"><see cref="InstrumentedProducerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        string? name,
        InstrumentedProducerBuilder<TKey, TValue> producerBuilder)
    {
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(producerBuilder);

        return AddKafkaProducerInstrumentation(builder, name: name, producerBuilder, configure: null);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="producerBuilder">Optional <see cref="InstrumentedProducerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <param name="configure">Optional callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaProducerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        string? name,
        InstrumentedProducerBuilder<TKey, TValue>? producerBuilder,
        Action<ConfluentKafkaInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.AddKafkaInstrumentationSharedServices();

        builder.ConfigureServices(services =>
        {
            if (configure != null)
            {
                services.Configure(name, configure);
            }

            services.Configure<ConfluentKafkaProducerInstrumentationOptions<TKey, TValue>>(name, options =>
            {
                options.Traces = true;
            });
        });

        return builder
            .AddSource(ConfluentKafkaCommon.ActivitySourceName)
            .AddInstrumentation(sp =>
            {
                var instrumentation = sp.GetRequiredService<ConfluentKafkaInstrumentation>();

                producerBuilder ??= sp.GetService<InstrumentedProducerBuilder<TKey, TValue>>();

                if (producerBuilder != null)
                {
                    var options = sp.GetRequiredService<IOptionsMonitor<ConfluentKafkaProducerInstrumentationOptions<TKey, TValue>>>();
                    producerBuilder.Options = options.Get(name);
                    instrumentation.AddProducer(name, producerBuilder);
                }

                return instrumentation;
            });
    }
}
