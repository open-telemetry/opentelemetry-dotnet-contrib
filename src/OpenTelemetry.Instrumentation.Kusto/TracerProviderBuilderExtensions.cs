// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        AddKustoInstrumentation(builder, configureKustoTraceInstrumentationOptions: null);

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configureKustoTraceInstrumentationOptions">Callback action for configuring <see cref="KustoTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKustoInstrumentation(
        this TracerProviderBuilder builder,
        Action<KustoTraceInstrumentationOptions>? configureKustoTraceInstrumentationOptions)
    {
        Guard.ThrowIfNull(builder);

        if (configureKustoTraceInstrumentationOptions != null)
        {
            builder.ConfigureServices(services => services.Configure(configureKustoTraceInstrumentationOptions));
        }

        // Be sure to eagerly initialize the instrumentation, as we must set environment variables before any clients are created.
        KustoInstrumentation.Initialize();

        builder.AddInstrumentation(sp =>
        {
            KustoInstrumentation.TraceOptions = sp.GetRequiredService<IOptionsMonitor<KustoTraceInstrumentationOptions>>().CurrentValue;
            return KustoInstrumentation.HandleManager.AddTracingHandle();
        });

        builder.AddSource(KustoActivitySourceHelper.ActivitySourceName);

        return builder;
    }
}
