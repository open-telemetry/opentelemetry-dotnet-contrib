// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using OpenTelemetry.Instrumentation.StackExchangeRedis;
using OpenTelemetry.Internal;
using StackExchange.Redis;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddRedisInstrumentation(this TracerProviderBuilder builder) => AddRedisInstrumentation(builder, connection: null, configure: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <remarks>
    /// Note: If an <see cref="IConnectionMultiplexer"/> is not supplied
    /// using the <paramref name="connection"/> parameter it will be
    /// resolved using the application <see cref="IServiceProvider"/>.
    /// </remarks>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="connection">Optional <see cref="IConnectionMultiplexer"/> to instrument.</param>
    /// <param name="configure">Optional callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddRedisInstrumentation(
        this TracerProviderBuilder builder,
        IConnectionMultiplexer connection,
        Action<StackExchangeRedisCallsInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        if (builder is not IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            if (connection == null)
            {
                throw new NotSupportedException($"StackExchange.Redis {nameof(IConnectionMultiplexer)} must be supplied when dependency injection is unavailable - to enable dependency injection use the OpenTelemetry.Extensions.Hosting package");
            }

            return AddRedisInstrumentation(builder, new StackExchangeRedisCallsInstrumentationOptions(), configure, null, connection);
        }

        return deferredTracerProviderBuilder.Configure((sp, builder) =>
        {
            if (connection == null)
            {
                connection = (IConnectionMultiplexer)sp.GetService(typeof(IConnectionMultiplexer));
                if (connection == null)
                {
                    throw new InvalidOperationException($"StackExchange.Redis {nameof(IConnectionMultiplexer)} could not be resolved through application {nameof(IServiceProvider)}");
                }
            }

            AddRedisInstrumentation(
                builder,
                sp.GetOptions<StackExchangeRedisCallsInstrumentationOptions>(),
                configure,
                null,
                connection);
        });
    }

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="connectionInstrumenter">Instrumenter to instrument connections with.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddRedisInstrumentation(
        this TracerProviderBuilder builder,
        out IConnectionInstrumenter connectionInstrumenter) => AddRedisInstrumentation(builder, out connectionInstrumenter, configure: null);

    /// <summary>
    /// Enables automatic data collection of outgoing requests to Redis.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="connectionInstrumenter">Instrumenter to instrument connections with.</param>
    /// <param name="configure">Optional callback to configure options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddRedisInstrumentation(
        this TracerProviderBuilder builder,
        out IConnectionInstrumenter connectionInstrumenter,
        Action<StackExchangeRedisCallsInstrumentationOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        var defaultInstrumenter = new ConnectionInstrumenter();
        connectionInstrumenter = defaultInstrumenter;

        if (builder is not IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            return AddRedisInstrumentation(builder, new StackExchangeRedisCallsInstrumentationOptions(), configure, defaultInstrumenter);
        }

        return deferredTracerProviderBuilder.Configure((sp, builder) =>
        {
            AddRedisInstrumentation(
                builder,
                sp.GetOptions<StackExchangeRedisCallsInstrumentationOptions>(),
                configure,
                defaultInstrumenter);
        });
    }

    private static TracerProviderBuilder AddRedisInstrumentation(
        TracerProviderBuilder builder,
        StackExchangeRedisCallsInstrumentationOptions options,
        Action<StackExchangeRedisCallsInstrumentationOptions> configure,
        ConnectionInstrumenter instrumenter,
        IConnectionMultiplexer connection = null)
    {
        configure?.Invoke(options);

        StackExchangeRedisCallsInstrumentation stackExchangeRedisCallsInstrumentation = null;

        var redisInstrumentation = builder
            .AddInstrumentation(() =>
            {
                stackExchangeRedisCallsInstrumentation = new StackExchangeRedisCallsInstrumentation(options);
                connection?.RegisterProfiler(stackExchangeRedisCallsInstrumentation?.GetProfilerSessionsFactory());

                return stackExchangeRedisCallsInstrumentation;
            })
            .AddSource(StackExchangeRedisCallsInstrumentation.ActivitySourceName);

        instrumenter?.Initialize(connectionMultiplexer => connectionMultiplexer.RegisterProfiler(stackExchangeRedisCallsInstrumentation?.GetProfilerSessionsFactory()));
        return redisInstrumentation;
    }
}
