// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of Kusto instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKustoInstrumentation(this TracerProviderBuilder builder) =>
        AddKustoInstrumentation(builder, options => { });

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configureKustoInstrumentationOptions">Callback action for configuring <see cref="KustoInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKustoInstrumentation(
        this TracerProviderBuilder builder,
        Action<KustoInstrumentationOptions> configureKustoInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(configureKustoInstrumentationOptions);

        configureKustoInstrumentationOptions(KustoInstrumentation.Options);

        builder.AddInstrumentation(sp =>
        {
            KustoInstrumentation.Initialize();
            return KustoInstrumentation.HandleManager.AddTracingHandle();
        });

        builder.AddSource(KustoActivitySourceHelper.ActivitySourceName);

        return builder;
    }
}
