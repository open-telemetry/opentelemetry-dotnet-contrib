// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.ConfluentKafka;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of Kafka instrumentation.
/// </summary>
public static partial class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this MeterProviderBuilder builder)
        => AddKafkaConsumerInstrumentation<TKey, TValue>(builder, name: null, consumerBuilder: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Kafka.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="consumerBuilder"><see cref="InstrumentedConsumerBuilder{TKey,TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this MeterProviderBuilder builder,
        InstrumentedConsumerBuilder<TKey, TValue> consumerBuilder)
    {
        Guard.ThrowIfNull(consumerBuilder);

        return AddKafkaConsumerInstrumentation(builder, name: null, consumerBuilder);
    }

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="name">The name of the instrumentation.</param>
    /// <param name="consumerBuilder"><see cref="InstrumentedConsumerBuilder{TKey,TValue}"/> to instrument.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaConsumerInstrumentation<TKey, TValue>(
        this MeterProviderBuilder builder,
        string? name,
        InstrumentedConsumerBuilder<TKey, TValue>? consumerBuilder)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            services.Configure<ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>>(name, EnableMetrics);
        });

        return builder
            .AddMeter(ConfluentKafkaCommon.InstrumentationName)
            .AddInstrumentation(sp =>
            {
                if (consumerBuilder == null)
                {
                    consumerBuilder = sp.GetRequiredService<InstrumentedConsumerBuilder<TKey, TValue>>();
                    var options = sp.GetRequiredService<IOptionsMonitor<ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>>>();
                    consumerBuilder.Options = options.Get(name);
                }

                if (consumerBuilder.Options == null)
                {
                    consumerBuilder.Options = new ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue>();
                    EnableMetrics(consumerBuilder.Options);
                }

                return new ConfluentKafkaConsumerInstrumentation<TKey, TValue>(consumerBuilder);
            });
    }

    private static void EnableMetrics<TKey, TValue>(ConfluentKafkaConsumerInstrumentationOptions<TKey, TValue> options) =>
        options.Metrics = true;
}
