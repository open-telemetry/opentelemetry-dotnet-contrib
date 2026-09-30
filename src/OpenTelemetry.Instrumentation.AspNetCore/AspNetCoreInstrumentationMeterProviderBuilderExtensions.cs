// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#else
using OpenTelemetry.Instrumentation.AspNetCore;
#endif
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
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

#if NET
        if (Environment.Version.Major < 11)
        {
            // ASP.NET Core 11+ natively sets error.type for 5xx responses. For earlier versions it is
            // added via middleware. This only takes effect when the MeterProvider is registered in the
            // application's IServiceCollection (for example via AddOpenTelemetry().WithMetrics(...)).
            builder.ConfigureServices(services =>
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, HttpServerErrorTypeStartupFilter>()));
        }

        return builder.ConfigureMeters();
#else
        // Note: Warm-up the status code and method mapping.
        _ = TelemetryHelper.BoxedStatusCodes;
        _ = TelemetryHelper.RequestDataHelper;

        builder.AddMeter(HttpInMetricsListener.Meter.Name);

#pragma warning disable CA2000
        builder.AddInstrumentation(new AspNetCoreMetrics());
#pragma warning restore CA2000

        return builder;
#endif
    }

#if NET
    internal static MeterProviderBuilder ConfigureMeters(this MeterProviderBuilder builder)
    {
        // There is no cost to listen for meters that aren't used. For example, listening for Kestrel meter in an app that doesn't use Kestrel is fine.
        // Listen for all built-in ASP.NET Core meters so metrics automatically light up depending on what an app does.
        var builtInAspNetCoreMeters = new[]
        {
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "Microsoft.AspNetCore.Http.Connections",
            "Microsoft.AspNetCore.Routing",
            "Microsoft.AspNetCore.Diagnostics",
            "Microsoft.AspNetCore.RateLimiting",
            "Microsoft.AspNetCore.Components",
            "Microsoft.AspNetCore.Components.Server.Circuits",
            "Microsoft.AspNetCore.Components.Lifecycle",
            "Microsoft.AspNetCore.Authorization",
            "Microsoft.AspNetCore.Authentication",
            "Microsoft.AspNetCore.Identity",
            "Microsoft.AspNetCore.MemoryPool",
        };

        return builder.AddMeter(builtInAspNetCoreMeters);
    }
#endif
}
