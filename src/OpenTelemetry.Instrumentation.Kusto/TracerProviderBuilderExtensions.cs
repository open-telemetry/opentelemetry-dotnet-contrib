// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Kusto;
using OpenTelemetry.Instrumentation.Kusto.Implementation;
using OpenTelemetry.Internal;
using KustoUtils = Kusto.Cloud.Platform.Utils;

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
    {
        Guard.ThrowIfNull(builder);

        return builder.AddKustoInstrumentation(new KustoInstrumentationOptions());
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

        Environment.SetEnvironmentVariable("KUSTO_DATA_TRACE_REQUEST_BODY", "1");
        KustoUtils.TraceSourceManager.AddTraceListener(new KustoListener(), startupDone: true);

        return builder;
    }
}
