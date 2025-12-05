// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class KustoInstrumentation
{
    private static readonly Lazy<ITraceListener> Listener = new(() =>
    {
        Environment.SetEnvironmentVariable("KUSTO_DATA_TRACE_REQUEST_BODY", "1");

        var listener = new KustoTraceRecordListener();
        TraceSourceManager.AddTraceListener(listener, startupDone: true);

        return listener;
    });

    public static KustoInstrumentationOptions Options { get; } = new KustoInstrumentationOptions();

    public static InstrumentationHandleManager HandleManager { get; } = new InstrumentationHandleManager();

    public static void Initialize() => _ = Listener.Value;
}
