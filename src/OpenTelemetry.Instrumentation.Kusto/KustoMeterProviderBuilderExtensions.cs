// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of Kusto instrumentation.
/// </summary>
public static class KustoMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKustoInstrumentation(this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        builder.AddInstrumentation(() =>
        {
            // TODO: Allow this to be configured
            var options = new KustoInstrumentationOptions();

            var listener = new KustoMetricListener(options);
            var handle = new ListenerHandle(listener);

            return handle;
        });

        builder.AddMeter(KustoActivitySourceHelper.MeterName);

        return builder;
    }
}
