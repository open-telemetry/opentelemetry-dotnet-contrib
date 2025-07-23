// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Instrumentation.AspNet.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Asp.Net Requests instrumentation.
/// </summary>
internal sealed class AspNetInstrumentation : IDisposable
{
    public static readonly AspNetInstrumentation Instance = new();

    public readonly InstrumentationHandleManager HandleManager = new();
    internal static readonly Assembly Assembly = typeof(HttpInListener).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name;
    internal static readonly string InstrumentationVersion = Assembly.GetPackageVersion();


    private readonly Meter meter;
    private readonly HttpInListener httpInListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
    /// </summary>
    private AspNetInstrumentation()
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);
        this.httpInListener = new HttpInListener(this.meter);
    }

    public AspNetTraceInstrumentationOptions TraceOptions { get; set; } = new();

    public AspNetMetricsInstrumentationOptions MetricOptions { get; set; } = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.meter?.Dispose();
        this.httpInListener?.Dispose();
    }
}
