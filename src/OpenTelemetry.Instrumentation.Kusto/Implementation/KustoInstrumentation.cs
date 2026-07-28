// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

/// <summary>
/// Holds the process-wide singleton <see cref="KustoTraceRecordListener"/> registered with the Kusto client
/// library. The options and active-handle state the listener uses live on the listener instance itself.
/// </summary>
internal static class KustoInstrumentation
{
    private static readonly Lazy<KustoTraceRecordListener> LazyListener = new(() =>
    {
        var listener = new KustoTraceRecordListener();
        TraceSourceManager.AddTraceListener(listener, startupDone: true);

        return listener;
    });

    /// <summary>
    /// Gets the singleton <see cref="KustoTraceRecordListener"/>, registering it with the Kusto client library
    /// on first access.
    /// </summary>
    public static KustoTraceRecordListener Listener => LazyListener.Value;
}
