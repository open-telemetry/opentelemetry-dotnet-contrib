// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of ASP.NET Core request instrumentation.
/// </summary>
public static class AspNetCoreInstrumentationMeterProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddAspNetCoreInstrumentation(
        this MeterProviderBuilder builder)
    {
        Guard.ThrowIfNull(builder);

        return builder
             .AddMeter("Microsoft.AspNetCore.Hosting")
             .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
             .AddMeter("Microsoft.AspNetCore.Http.Connections")
             .AddMeter("Microsoft.AspNetCore.Routing")
             .AddMeter("Microsoft.AspNetCore.Diagnostics")
             .AddMeter("Microsoft.AspNetCore.RateLimiting");
    }
}
