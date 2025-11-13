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
    public static TracerProviderBuilder AddKustoInstrumentation(this TracerProviderBuilder builder)
        => AddKustoInstrumentation(builder, new KustoInstrumentationOptions());

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

        var options = new KustoInstrumentationOptions();
        configureKustoInstrumentationOptions(options);
        return AddKustoInstrumentation(builder, options);
    }

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="options">Kusto instrumentation options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKustoInstrumentation(
        this TracerProviderBuilder builder,
        KustoInstrumentationOptions options)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(options);

        builder.AddInstrumentation(() =>
        {
            if (options.RecordQueryText)
            {
                Environment.SetEnvironmentVariable("KUSTO_DATA_TRACE_REQUEST_BODY", "1");
            }

            var listener = new KustoTraceListener(options);
            var handle = new ListenerHandle(listener);
            return handle;
        });

        builder.AddSource(KustoActivitySourceHelper.ActivitySourceName);

        return builder;
    }
}
