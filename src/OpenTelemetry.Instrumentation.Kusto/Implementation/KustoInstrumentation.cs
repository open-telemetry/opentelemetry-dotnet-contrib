// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Class to hold the singleton instances used for Kusto instrumentation.
/// </summary>
internal static class KustoInstrumentation
{
    private static readonly Lazy<ITraceListener> Listener = new(() =>
    {
        Environment.SetEnvironmentVariable("KUSTO_DATA_TRACE_REQUEST_BODY", "1");

        var listener = new KustoTraceRecordListener();
        TraceSourceManager.AddTraceListener(listener, startupDone: true);

        return listener;
    });

    /// <summary>
    /// Gets the post-configured options for Kusto instrumentation.
    /// </summary>
    public static KustoInstrumentationOptions Options { get; } = new KustoInstrumentationOptions();

    /// <summary>
    /// Gets the <see cref="InstrumentationHandleManager"/> that tracks if there are any active listeners for <see cref="KustoTraceRecordListener"/>.
    /// </summary>
    public static InstrumentationHandleManager HandleManager { get; } = new InstrumentationHandleManager();

    public static void Initialize() => _ = Listener.Value;
}
