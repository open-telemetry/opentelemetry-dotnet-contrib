// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNet.Implementation;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Asp.Net Requests instrumentation.
/// </summary>
internal sealed class AspNetInstrumentation : IDisposable
{
    public static readonly AspNetInstrumentation Instance = new();

    public readonly InstrumentationHandleManager HandleManager = new();
    private readonly HttpInListener httpInListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
    /// </summary>
    private AspNetInstrumentation()
    {
        this.httpInListener = new HttpInListener();
    }

    public AspNetTraceInstrumentationOptions TraceOptions { get; set; } = new();

    public AspNetMetricsInstrumentationOptions MetricOptions { get; set; } = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.httpInListener?.Dispose();
    }
}
