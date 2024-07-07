// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Owin;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of OWIN request instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables the incoming requests automatic data collection for OWIN.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOwinInstrumentation(this TracerProviderBuilder builder) =>
        AddOwinInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables the incoming requests automatic data collection for OWIN.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">OWIN Request configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOwinInstrumentation(
        this TracerProviderBuilder builder,
        Action<OwinInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var owinOptions = new OwinInstrumentationOptions();
        configure?.Invoke(owinOptions);

        OwinInstrumentationActivitySource.Options = owinOptions;

        return builder.AddSource(OwinInstrumentationActivitySource.ActivitySourceName);
    }
}
