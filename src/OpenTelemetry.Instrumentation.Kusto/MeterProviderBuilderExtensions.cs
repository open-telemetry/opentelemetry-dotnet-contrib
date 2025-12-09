// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of Kusto instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKustoInstrumentation(this MeterProviderBuilder builder) =>
        builder.AddKustoInstrumentation(configureKustoMeterInstrumentationOptions: null);

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configureKustoMeterInstrumentationOptions">Callback action for configuring <see cref="KustoMeterInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKustoInstrumentation(this MeterProviderBuilder builder, Action<KustoMeterInstrumentationOptions>? configureKustoMeterInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        if (configureKustoMeterInstrumentationOptions != null)
        {
            builder.ConfigureServices(services => services.Configure(configureKustoMeterInstrumentationOptions));
        }

        // Be sure to eagerly initialize the instrumentation, as we must set environment variables before any clients are created.
        KustoInstrumentation.Initialize();

        builder.AddInstrumentation(sp =>
        {
            KustoInstrumentation.MeterOptions = sp.GetRequiredService<IOptionsMonitor<KustoMeterInstrumentationOptions>>().CurrentValue;
            return KustoInstrumentation.HandleManager.AddMetricHandle();
        });

        builder.AddMeter(KustoActivitySourceHelper.MeterName);

        return builder;
    }
}
