// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
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
    => builder.AddEventCountersInstrumentation(null, null);

    /// <summary>
    /// Enables EventCounter instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configuration">The configuration section used to configure <see cref="EventCountersInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddEventCountersInstrumentation(
        this MeterProviderBuilder builder,
        IConfigurationSection configuration)
    => builder.AddEventCountersInstrumentation(null, configuration);

    /// <summary>
    /// Enables EventCounter instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">EventCounters instrumentation options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddEventCountersInstrumentation(
        this MeterProviderBuilder builder,
        Action<EventCountersInstrumentationOptions> configure)
    => builder.AddEventCountersInstrumentation(configure, null);

    /// <summary>
    /// Enables EventCounter instrumentation using configuration.
    /// </summary>
    /// <param name="builder">The <see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">EventCounters instrumentation options.</param>
    /// <param name="configuration">The configuration section used to configure <see cref="EventCountersInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    private static MeterProviderBuilder AddEventCountersInstrumentation(
        this MeterProviderBuilder builder,
        Action<EventCountersInstrumentationOptions>? configure = null,
        IConfigurationSection? configuration = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new EventCountersInstrumentationOptions();
        configure?.Invoke(options);

        // configuration?.Bind(options);

        if (configuration != null)
        {
            if (int.TryParse(configuration["EventCounters:RefreshIntervalSecs"], out var refreshInterval))
            {
                options.RefreshIntervalSecs = refreshInterval;
            }

            var eventSourceNames = configuration.GetSection("EventCounters:EventSourceNames").Get<string[]>();
            if (eventSourceNames != null)
            {
                options.AddEventSources(eventSourceNames);
            }
        }

        builder.AddMeter(EventCountersMetrics.MeterInstance.Name);
        return builder.AddInstrumentation(() => new EventCountersMetrics(options));
    }
}
