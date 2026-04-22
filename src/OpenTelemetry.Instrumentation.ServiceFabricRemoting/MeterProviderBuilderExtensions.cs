// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.ServiceFabricRemoting;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of ServiceFabric Remoting instrumentation.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables the RPC metrics for ServiceFabric Remoting.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddServiceFabricRemotingInstrumentation(this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder.AddMeter(ServiceFabricRemotingMetrics.MeterName);
    }
}
