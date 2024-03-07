// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry.Instrumentation.AspNet;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of ASP.NET request instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAspNetInstrumentation(this TracerProviderBuilder builder) =>
        AddAspNetInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables the incoming requests automatic data collection for ASP.NET.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">ASP.NET Request configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddAspNetInstrumentation(
        this TracerProviderBuilder builder,
        Action<AspNetTraceInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var aspnetOptions = new AspNetTraceInstrumentationOptions();
        configure?.Invoke(aspnetOptions);

        builder.AddInstrumentation(() => new AspNetInstrumentation(aspnetOptions));
        builder.AddSource(TelemetryHttpModule.AspNetSourceName);

        return builder;
    }
}
