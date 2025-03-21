// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNet.Implementation;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Asp.Net Requests instrumentation.
/// </summary>
internal sealed class AspNetInstrumentation : IDisposable
{
    private readonly HttpInListener httpInListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="AspNetInstrumentation"/> class.
    /// </summary>
    /// <param name="options">Configuration options for ASP.NET instrumentation.</param>
    public AspNetInstrumentation(AspNetTraceInstrumentationOptions options)
    {
        this.httpInListener = new HttpInListener(options);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.httpInListener?.Dispose();
    }
}
