// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.EventCounters;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables EventCounter instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddEventCountersInstrumentation(this MeterProviderBuilder builder)
        => AddEventCountersInstrumentation(builder, name: null, configure: null);

    /// <summary>
    /// Enables EventCounter instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">EventCounters instrumentation options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddEventCountersInstrumentation(
        this MeterProviderBuilder builder,
        Action<EventCountersInstrumentationOptions>? configure)
        => AddEventCountersInstrumentation(builder, name: null, configure: configure);

    /// <summary>
    /// Enables EventCounter instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="name">The name used when retrieving options.</param>
    /// <param name="configure">EventCounters instrumentation options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddEventCountersInstrumentation(
        this MeterProviderBuilder builder,
        string? name,
        Action<EventCountersInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (configure != null)
            {
                services.Configure(name, configure);
            }

            services.RegisterOptionsFactory(configuration => new EventCountersInstrumentationOptions(configuration));
        });

        if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            deferredMeterProviderBuilder.Configure((sp, builder) =>
            {
                AddEventCountersInstrumentationSources(builder, sp);
            });
        }

        return builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<EventCountersInstrumentationOptions>>().Get(name);
            return new EventCountersMetrics(options);
        });
    }

    private static void AddEventCountersInstrumentationSources(
        this MeterProviderBuilder builder,
        IServiceProvider? serviceProvider = null)
    {
        builder.AddMeter(EventCountersMetrics.MeterInstance.Name);
    }
}
