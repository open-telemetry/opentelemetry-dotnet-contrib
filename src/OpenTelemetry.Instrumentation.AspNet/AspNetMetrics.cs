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
internal sealed class AspNetMetrics : IDisposable
{
    internal static readonly Assembly Assembly = typeof(HttpInMetricsListener).Assembly;
    internal static readonly AssemblyName AssemblyName = Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name;
    internal static readonly string InstrumentationVersion = Assembly.GetPackageVersion();

    private readonly Meter meter;

    private readonly HttpInMetricsListener httpInMetricsListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetMetrics"/> class.
    /// </summary>
    /// <param name="options">The metrics configuration options.</param>
    public AspNetMetrics(AspNetMetricsInstrumentationOptions options)
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);
        this.httpInMetricsListener = new HttpInMetricsListener(this.meter, options);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.meter?.Dispose();
        this.httpInMetricsListener?.Dispose();
    }
}
