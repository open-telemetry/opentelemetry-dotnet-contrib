// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    public static TracerProviderBuilder AddKustoInstrumentation(this TracerProviderBuilder builder)
        => AddKustoInstrumentation(builder, options => { });

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
        Guard.ThrowIfNull(configureKustoInstrumentationOptions);
        return AddKustoInstrumentation(builder, name: null, configureKustoInstrumentationOptions);
    }

    // TODO: Revisit named options

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="configureOptions">Kusto instrumentation options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    private static TracerProviderBuilder AddKustoInstrumentation(
        this TracerProviderBuilder builder,
        string? name,
        Action<KustoInstrumentationOptions>? configureOptions)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configureOptions != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureOptions));
        }

        builder.AddInstrumentation(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<KustoInstrumentationOptions>>().Get(name);
            KustoInstrumentation.TracingOptions = options;

            KustoInstrumentation.InitializeTracing();
            return KustoInstrumentation.HandleManager.AddTracingHandle();
        });

        builder.AddSource(KustoActivitySourceHelper.ActivitySourceName);

        return builder;
    }
}
