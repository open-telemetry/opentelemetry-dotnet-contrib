// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Asp.Net Core Requests instrumentation.
/// </summary>
internal sealed class AspNetCoreMetrics : IDisposable
{
    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    internal AspNetCoreMetrics()
    {
        var metricsListener = new HttpInMetricsListener("Microsoft.AspNetCore");
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(metricsListener, AspNetCoreInstrumentation.IsEnabled, AspNetCoreInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
        => this.diagnosticSourceSubscriber?.Dispose();
}
#endif
