// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis;

/// <summary>
/// Extension methods to simplify registering of Redis instrumentation.
/// </summary>
public static class StackExchangeRedisInstrumentationExtensions
{
    /// <summary>
    /// Registers a callback for configuring Redis instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback to configure instrumentation.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder ConfigureRedisInstrumentation(
        this TracerProviderBuilder builder,
        Action<StackExchangeRedisInstrumentation> configure)
    {
        Guard.ThrowIfNull(configure);

        return ConfigureRedisInstrumentation(builder, (sp, instrumentation) => configure(instrumentation));
    }

    /// <summary>
    /// Registers a callback for configuring Redis instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback to configure instrumentation.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder ConfigureRedisInstrumentation(
        this TracerProviderBuilder builder,
        Action<IServiceProvider, StackExchangeRedisInstrumentation> configure)
    {
        Guard.ThrowIfNull(configure);

        if (builder is not IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            throw new NotSupportedException("ConfigureRedisInstrumentation is not supported on the supplied builder type.");
        }

        builder.AddRedisInstrumentationSharedServices();

        deferredTracerProviderBuilder.Configure(
            (sp, builder) => configure(sp, sp.GetRequiredService<StackExchangeRedisInstrumentation>()));

        return builder;
    }

    internal static TracerProviderBuilder AddRedisInstrumentationSharedServices(
        this TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.ConfigureServices(AddRedisInstrumentationSharedServices);
    }

    internal static MeterProviderBuilder AddRedisInstrumentationSharedServices(
        this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.ConfigureServices(AddRedisInstrumentationSharedServices);
    }

    internal static void AddRedisInstrumentationSharedServices(IServiceCollection services)
    {
        services.TryAddSingleton(
                sp => new StackExchangeRedisInstrumentation(
                    sp.GetRequiredService<IOptionsMonitor<StackExchangeRedisInstrumentationOptions>>()));
    }

    internal static TracerProviderBuilder AddInstrumentation(
        this TracerProviderBuilder builder,
        string name,
        IConnectionMultiplexer? connection,
        object? serviceKey)
    {
        return builder
            .AddInstrumentation(InstrumentationFactory(name, connection, serviceKey));
    }

    internal static MeterProviderBuilder AddInstrumentation(
        this MeterProviderBuilder builder,
        string name,
        IConnectionMultiplexer? connection,
        object? serviceKey)
    {
        return builder
            .AddInstrumentation(InstrumentationFactory(name, connection, serviceKey));
    }

    internal static Func<IServiceProvider, StackExchangeRedisInstrumentation> InstrumentationFactory(
        string name,
        IConnectionMultiplexer? connection,
        object? serviceKey)
    {
        return sp =>
        {
            var instrumentation = sp.GetRequiredService<StackExchangeRedisInstrumentation>();

            connection ??= serviceKey == null
                ? sp.GetService<IConnectionMultiplexer>()
                : sp.GetKeyedService<IConnectionMultiplexer>(serviceKey);

            if (connection != null)
            {
                instrumentation.AddConnection(name, connection);
            }

            return instrumentation;
        };
    }
}
