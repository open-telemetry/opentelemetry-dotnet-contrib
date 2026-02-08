// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.SurrealDb;
using OpenTelemetry.Instrumentation.SurrealDb.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class SurrealDbMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables SurrealDbClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddSurrealDbInstrumentation(
        this MeterProviderBuilder builder
    )
    {
        Guard.ThrowIfNull(builder);

        builder.AddInstrumentation(sp =>
        {
            return SurrealDbInstrumentation.Instance.HandleManager.AddMetricHandle();
        });

        builder.AddMeter(SurrealDbTelemetryHelper.Meter.Name);

        return builder;
    }
}
