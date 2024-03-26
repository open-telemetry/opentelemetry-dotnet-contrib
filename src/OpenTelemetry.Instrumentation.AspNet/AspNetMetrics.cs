// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Instrumentation.AspNet.Implementation;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Asp.Net Requests instrumentation.
/// </summary>
internal sealed class AspNetMetrics : IDisposable
{
    internal static readonly AssemblyName AssemblyName = typeof(HttpInMetricsListener).Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name;
    internal static readonly string InstrumentationVersion = ActivitySourceVersionHelper.GetVersion<AspNetMetrics>();

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
