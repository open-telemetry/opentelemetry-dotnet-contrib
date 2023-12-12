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
    public static TracerProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder)
        => AddKafkaConsumerInstrumentation<TKey, TValue>(builder, name: null, consumerBuilder: null, configure: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="consumerBuilder"><see cref="InstrumentedConsumerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder)
    {
        Guard.ThrowIfNull(consumerBuilder);

        return AddKafkaConsumerInstrumentation(builder, name: null, consumerBuilder, configure: null);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        Action<ConfluentKafkaInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        return AddKafkaConsumerInstrumentation<TKey, TValue>(builder, name: null, consumerBuilder: null, configure);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="consumerBuilder"><see cref="InstrumentedConsumerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <param name="configure">Callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder,
        Action<ConfluentKafkaInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(consumerBuilder);
        Guard.ThrowIfNull(configure);

        return AddKafkaConsumerInstrumentation(builder, name: null, consumerBuilder, configure);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="consumerBuilder"><see cref="InstrumentedConsumerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        string? name,
        InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder)
    {
        Guard.ThrowIfNull(name);
        Guard.ThrowIfNull(consumerBuilder);

        return AddKafkaConsumerInstrumentation(builder, name: name, consumerBuilder, configure: null);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="consumerBuilder">Optional <see cref="InstrumentedConsumerBuilder{TKey, TValue}"/> to instrument.</param>
    /// <param name="configure">Optional callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this TracerProviderBuilder builder,
        string? name,
        InstrumentedConsumerBuilder<TKey, TValue>? consumerBuilder,
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

            services.Configure<ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>>(name, options =>
            {
                options.Traces = true;
            });
        });

        return builder
            .AddSource(ConfluentKafkaCommon.ActivitySourceName)
            .AddInstrumentation(sp =>
            {
                var instrumentation = sp.GetRequiredService<ConfluentKafkaInstrumentation>();

                consumerBuilder ??= sp.GetService<InstrumentedConsumerBuilder<TKey, TValue>>();

                if (consumerBuilder != null)
                {
                    var options = sp.GetRequiredService<IOptionsMonitor<ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>>>();
                    consumerBuilder.Options = options.Get(name);
                    instrumentation.AddConsumer(name, consumerBuilder);
                }

                return instrumentation;
            });
    }
}
