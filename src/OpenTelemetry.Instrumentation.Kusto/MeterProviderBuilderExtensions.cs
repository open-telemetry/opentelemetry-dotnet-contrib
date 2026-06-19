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
        builder.AddKustoInstrumentation(configure: null);

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback action for configuring <see cref="KustoMeterInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKustoInstrumentation(
        this MeterProviderBuilder builder,
        Action<KustoMeterInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(configure));
        }

        // Eagerly register the trace listener with the Kusto client library so it is in place before any clients are created.
        KustoInstrumentation.Initialize();

        builder.AddInstrumentation(sp =>
        {
            KustoInstrumentation.MeterOptions = sp.GetRequiredService<IOptionsMonitor<KustoMeterInstrumentationOptions>>().CurrentValue;
            KustoInstrumentationEventSource.Log.WarnIfQueryTextCaptureNotEnabled(KustoInstrumentation.MeterOptions.RecordQueryText, KustoInstrumentation.MeterOptions.RecordQuerySummary);
            return KustoInstrumentation.HandleManager.AddMetricHandle();
        });

        builder.AddMeter(KustoActivitySourceHelper.MeterName);

        return builder;
    }
}
