// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto;
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
        builder.AddKustoInstrumentation(options => { });

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configureKustoInstrumentationOptions">Action to configure the <see cref="KustoInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKustoInstrumentation(this MeterProviderBuilder builder, Action<KustoInstrumentationOptions> configureKustoInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(configureKustoInstrumentationOptions);

        configureKustoInstrumentationOptions(KustoInstrumentation.Options);

        builder.AddInstrumentation(sp =>
        {
            KustoInstrumentation.Initialize();
            return KustoInstrumentation.HandleManager.AddMetricHandle();
        });

        builder.AddMeter(KustoActivitySourceHelper.MeterName);

        return builder;
    }
}
