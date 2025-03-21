// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Web;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNet;

/// <summary>
/// Stores options for the <see cref="TelemetryHttpModule"/>.
/// </summary>
public class TelemetryHttpModuleOptions
{
    private TextMapPropagator textMapPropagator = new TraceContextPropagator();

    internal TelemetryHttpModuleOptions()
    {
    }

    /// <summary>
    /// Gets or sets the <see cref=" Context.Propagation.TextMapPropagator"/> to use to
    /// extract <see cref="PropagationContext"/> from incoming requests.
    /// </summary>
    public TextMapPropagator TextMapPropagator
    {
        get => this.textMapPropagator;
        set
        {
            Guard.ThrowIfNull(value);

            this.textMapPropagator = value;
        }
    }

    /// <summary>
    /// Gets or sets a callback action to be fired when a request is started.
    /// </summary>
    public Action<Activity, HttpContext>? OnRequestStartedCallback { get; set; }

    /// <summary>
    /// Gets or sets a callback action to be fired when a request is stopped.
    /// </summary>
    public Action<Activity, HttpContext>? OnRequestStoppedCallback { get; set; }

    /// <summary>
    /// Gets or sets a callback action to be fired when an unhandled
    /// exception is thrown processing a request.
    /// </summary>
    public Action<Activity, HttpContext, Exception>? OnExceptionCallback { get; set; }
}
