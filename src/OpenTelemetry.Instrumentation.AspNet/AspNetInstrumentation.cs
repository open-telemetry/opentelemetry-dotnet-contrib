// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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

    public static readonly Assembly Assembly = typeof(HttpInListener).Assembly;
    public static readonly AssemblyName AssemblyName = Assembly.GetName();
    public static readonly string MeterName = AssemblyName.Name!;
    public static readonly string ActivitySourceName = AssemblyName.Name;
    public static readonly Meter Meter = new(MeterName, Assembly.GetPackageVersion());
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Assembly.GetPackageVersion());
    public static readonly Histogram<double> HttpServerDuration = Meter.CreateHistogram(
        "http.server.request.duration",
        unit: "s",
        description: "Duration of HTTP server requests.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10] });

    public readonly InstrumentationHandleManager HandleManager = new();
    private readonly HttpInListener httpInListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
    /// </summary>
    private AspNetInstrumentation()
    {
        this.httpInListener = new();
    }

    public AspNetTraceInstrumentationOptions TraceOptions { get; set; } = new();

    public AspNetMetricsInstrumentationOptions MetricOptions { get; set; } = new();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.httpInListener?.Dispose();
    }
}
