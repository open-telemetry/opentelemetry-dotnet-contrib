// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation;
using OpenTelemetry.Internal;
using StackExchange.Redis;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of StackExchangeRedis request instrumentation.
/// </summary>
public static class StackExchangeRedisMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <remarks>
    /// Note: A <see cref="IConnectionMultiplexer"/> will be resolved using the
    /// application <see cref="IServiceProvider"/>.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRedisInstrumentation(
        this MeterProviderBuilder builder)
        => AddRedisInstrumentation(builder, name: null, connection: null, serviceKey: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="connection"><see cref="IConnectionMultiplexer"/> to instrument.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRedisInstrumentation(
        this MeterProviderBuilder builder,
        IConnectionMultiplexer connection)
    {
        Guard.ThrowIfNull(connection);

        return AddRedisInstrumentation(builder, name: null, connection, serviceKey: null);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="serviceKey">Optional service key used to retrieve the <see cref="IConnectionMultiplexer"/> to instrument from the <see cref="IServiceProvider" />.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRedisInstrumentation(
        this MeterProviderBuilder builder,
        object serviceKey)
    {
        Guard.ThrowIfNull(serviceKey);

        return AddRedisInstrumentation(builder, name: null, connection: null, serviceKey);
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <remarks>
    /// Note: If an <see cref="IConnectionMultiplexer"/> is not supplied
    /// using the <paramref name="connection"/> parameter it will be
    /// resolved using the application <see cref="IServiceProvider"/>.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="connection">Optional <see cref="IConnectionMultiplexer"/> to instrument.</param>
    /// <param name="serviceKey">Optional service key used to retrieve the <see cref="IConnectionMultiplexer"/> to instrument from the <see cref="IServiceProvider" />.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddRedisInstrumentation(
        this MeterProviderBuilder builder,
        string? name,
        IConnectionMultiplexer? connection,
        object? serviceKey)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.AddRedisInstrumentationSharedServices();

        return builder
            .AddMeter(RedisMetrics.InstrumentationName)
            .AddInstrumentation(name, connection, serviceKey);
    }
}
