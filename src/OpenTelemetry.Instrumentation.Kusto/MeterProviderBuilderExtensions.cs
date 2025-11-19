// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Cloud.Platform.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Kusto;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

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
    public static MeterProviderBuilder AddKustoInstrumentation(this MeterProviderBuilder builder)
        => AddKustoInstrumentation(builder, options => { });

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configureKustoInstrumentationOptions">Action to configure the <see cref="KustoInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKustoInstrumentation(this MeterProviderBuilder builder, Action<KustoInstrumentationOptions> configureKustoInstrumentationOptions)
    {
        Guard.ThrowIfNull(configureKustoInstrumentationOptions);

        return AddKustoInstrumentation(builder, name: null, configureKustoInstrumentationOptions);
    }

    // TODO: Revisit named options

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="configureOptions">Kusto instrumentation options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    private static MeterProviderBuilder AddKustoInstrumentation(
        this MeterProviderBuilder builder,
        string? name,
        Action<KustoInstrumentationOptions>? configureOptions)
    {
        Guard.ThrowIfNull(builder);
        name ??= Options.DefaultName;

        if (configureOptions != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureOptions));
        }

        builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<KustoInstrumentationOptions>>().Get(name);
            KustoInstrumentation.MetricOptions = options;

            KustoInstrumentation.InitializeMetrics();
            return KustoInstrumentation.HandleManager.AddMetricHandle();
        });

        builder.AddMeter(KustoActivitySourceHelper.MeterName);

        return builder;
    }
}
