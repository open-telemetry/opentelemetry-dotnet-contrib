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
        AddKustoInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables Kusto instrumentation.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback action for configuring <see cref="KustoTraceInstrumentationOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKustoInstrumentation(
        this TracerProviderBuilder builder,
        Action<KustoTraceInstrumentationOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(configure));
        }

        // Accessing Listener registers the trace listener with the Kusto client library, so it is in place before any clients are created.
        var listener = KustoInstrumentation.Listener;

        builder.AddInstrumentation(sp =>
        {
            listener.TraceOptions = sp.GetRequiredService<IOptionsMonitor<KustoTraceInstrumentationOptions>>().CurrentValue;
            KustoInstrumentationEventSource.Log.WarnIfQueryTextCaptureNotEnabled(listener.TraceOptions.RecordQueryText, listener.TraceOptions.RecordQuerySummary);
            return listener.HandleManager.AddTracingHandle();
        });

        builder.AddSource(KustoActivitySourceHelper.ActivitySourceName);

        return builder;
    }
}
