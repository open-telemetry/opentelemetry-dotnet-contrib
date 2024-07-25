// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNet;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of ASP.NET request instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetInstrumentation(this MeterProviderBuilder builder) =>
        AddAspNetInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback action for configuring <see cref="AspNetMetricsInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetInstrumentation(
        this MeterProviderBuilder builder,
        Action<AspNetMetricsInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        return builder.ConfigureServices(services =>
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.ConfigureOpenTelemetryMeterProvider((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<AspNetMetricsInstrumentationOptions>>().Get(name: null);

                builder.AddInstrumentation(() => new AspNetMetrics(options));
                builder.AddMeter(AspNetMetrics.InstrumentationName);
            });
        });
    }
}
