// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Kusto.Cloud.Platform.Utils;

namespace OpenTelemetry.Instrumentation.Kusto.Implementation;

internal static class KustoInstrumentation
{
    private static readonly Lazy<ITraceListener> TraceListener = new(() =>
    {
        Environment.SetEnvironmentVariable("KUSTO_DATA_TRACE_REQUEST_BODY", "1");

        var listener = new KustoTraceListener();
        TraceSourceManager.AddTraceListener(listener, startupDone: true);

        return listener;
    });

    private static readonly Lazy<ITraceListener> MetricListener = new(() =>
    {
        Environment.SetEnvironmentVariable("KUSTO_DATA_TRACE_REQUEST_BODY", "1");

        var listener = new KustoMetricListener();
        TraceSourceManager.AddTraceListener(listener, startupDone: true);

        return listener;
    });

    public static KustoInstrumentationOptions TracingOptions { get; set; } = new KustoInstrumentationOptions();

    public static KustoInstrumentationOptions MetricOptions { get; set; } = new KustoInstrumentationOptions();

    public static InstrumentationHandleManager HandleManager { get; } = new InstrumentationHandleManager();

    public static void InitializeTracing()
    {
        _ = TraceListener.Value;
    }

    public static void InitializeMetrics()
    {
        _ = MetricListener.Value;
    }
}
