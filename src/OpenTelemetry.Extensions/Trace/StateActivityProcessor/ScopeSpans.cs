// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.StateActivityProcessor;

/// <summary>
/// ScopeSpans per spec.
/// </summary>
public class ScopeSpans
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeSpans"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal whether it is start or stop.</param>
    public ScopeSpans(Activity activity, TracesData.Signal signal)
    {
        this.Scope = new InstrumentationScope(activity);
        this.Spans.Add(new Span(activity, signal));
    }

    /// <summary>
    /// Gets or sets the instrumentation scope.
    /// </summary>
    public InstrumentationScope? Scope { get; set; }

    /// <summary>
    /// Gets the spans in this scope.
    /// </summary>
    public Collection<Span> Spans { get; } = [];
}
