// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.ServiceFabricRemoting;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of ServiceFabric Remoting instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables the RPC metrics for ServiceFabric Remoting (<c>rpc.server.call.duration</c> and <c>rpc.client.call.duration</c>).
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddServiceFabricRemotingInstrumentation(this MeterProviderBuilder builder)
    {
        return AddServiceFabricRemotingInstrumentation(builder, configure: null);
    }

    /// <summary>
    /// Enables the RPC metrics for ServiceFabric Remoting (<c>rpc.server.call.duration</c> and <c>rpc.client.call.duration</c>).
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">ServiceFabric Remoting configuration options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddServiceFabricRemotingInstrumentation(this MeterProviderBuilder meterProviderBuilder, Action<ServiceFabricRemotingInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(meterProviderBuilder);

        return meterProviderBuilder.ConfigureServices(services =>
        {
            if (configure != null)
            {
                services.Configure(configure);
            }

            services.ConfigureOpenTelemetryMeterProvider((serviceProvider, builder) =>
            {
                ServiceFabricRemotingActivitySource.Options = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceFabricRemotingInstrumentationOptions>>().Get(name: null);

                builder.AddMeter(ServiceFabricRemotingMetrics.Meter.Name);
            });
        });
    }
}
