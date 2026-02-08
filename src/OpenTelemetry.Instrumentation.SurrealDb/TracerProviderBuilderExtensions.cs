// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.SurrealDb;
using OpenTelemetry.Instrumentation.SurrealDb.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables SurrealDbClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddSurrealDbInstrumentation(
        this TracerProviderBuilder builder
    ) =>
        AddSurrealDbInstrumentation(
            builder,
            name: null,
            configureSurrealDbTraceInstrumentationOptions: null
        );

    /// <summary>
    /// Enables SurrealDbClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="SurrealDbTraceInstrumentationOptions">Callback action for configuring <see cref="SurrealDbTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddSurrealDbInstrumentation(
        this TracerProviderBuilder builder,
        Action<SurrealDbTraceInstrumentationOptions> configureSurrealDbTraceInstrumentationOptions
    ) =>
        AddSurrealDbInstrumentation(
            builder,
            name: null,
            configureSurrealDbTraceInstrumentationOptions
        );

    /// <summary>
    /// Enables SurrealDbClient instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureSurrealDbTraceInstrumentationOptions">Callback action for configuring <see cref="SurrealDbTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddSurrealDbInstrumentation(
        this TracerProviderBuilder builder,
        string? name,
        Action<SurrealDbTraceInstrumentationOptions>? configureSurrealDbTraceInstrumentationOptions
    )
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configureSurrealDbTraceInstrumentationOptions != null)
        {
            builder.ConfigureServices(services =>
                services.Configure(name, configureSurrealDbTraceInstrumentationOptions)
            );
        }

        builder.AddInstrumentation(sp =>
        {
            var surrealDbOptions = sp.GetRequiredService<
                IOptionsMonitor<SurrealDbTraceInstrumentationOptions>
            >()
                .Get(name);
            SurrealDbInstrumentation.TracingOptions = surrealDbOptions;
            return SurrealDbInstrumentation.Instance.HandleManager.AddTracingHandle();
        });

        string[] sources = [SurrealDbTelemetryHelper.ActivitySource.Name, SurrealDbTelemetryHelper.SurrealDbSystemName];
        builder.AddSource(sources);

        return builder;
    }
}
