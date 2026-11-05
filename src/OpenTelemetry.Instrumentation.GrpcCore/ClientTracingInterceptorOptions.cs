// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// Options for the ClientTracingInterceptor.
/// </summary>
public class ClientTracingInterceptorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether or not to record individual message events.
    /// <para/>
    /// At most 128 message events are recorded for each call; any further messages are not recorded.
    /// </summary>
    public bool RecordMessageEvents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the exception will be recorded as ActivityEvent or not.
    /// </summary>
    public bool RecordException { get; set; }

    /// <summary>
    /// Gets the propagator.
    /// </summary>
    public TextMapPropagator Propagator { get; internal set; } = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Gets or sets additional activity tags used during unit testing.
    /// </summary>
    internal IReadOnlyDictionary<string, object?>? AdditionalTags { get; set; }
}
