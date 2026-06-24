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
    /// <remarks>
    /// The Kusto instrumentation uses a single, process-wide trace listener, so all providers share one set of
    /// options. When multiple providers configure the instrumentation, the most recent call wins.
    /// </remarks>
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

        // Accessing Listener registers the trace listener with the Kusto client library, so it is in place before any clients are created.
        var listener = KustoInstrumentation.Listener;

        builder.AddInstrumentation(sp =>
        {
            listener.MeterOptions = sp.GetRequiredService<IOptionsMonitor<KustoMeterInstrumentationOptions>>().CurrentValue;

            // Read the variable directly rather than from configuration, because the Kusto client reads it the
            // same way when it decides whether to emit the query text.
            if ((listener.MeterOptions.RecordQueryText || listener.MeterOptions.RecordQuerySummary)
                && Environment.GetEnvironmentVariable(KustoInstrumentationEventSource.TraceRequestBodyEnvironmentVariable) != "1")
            {
                KustoInstrumentationEventSource.Log.QueryTextCaptureNotEnabled();
            }

            return listener.HandleManager.AddMetricHandle();
        });

        builder.AddMeter(KustoMetrics.Meter.Name);

        return builder;
    }
}
