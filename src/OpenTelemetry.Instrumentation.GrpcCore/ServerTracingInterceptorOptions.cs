// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// Options for the ServerTracingInterceptor.
/// </summary>
public class ServerTracingInterceptorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not to record individual message events.
    /// </summary>
    public bool RecordMessageEvents { get; set; }

    /// <summary>
    /// Gets the propagator.
    /// </summary>
    public TextMapPropagator Propagator { get; internal set; } = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Gets or sets a custom identfier used during unit testing.
    /// </summary>
    internal Guid ActivityIdentifierValue { get; set; }
}
