// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.PartialActivityProcessor;

/// <summary>
/// ResourceSpans per spec.
/// </summary>
public class ResourceSpans
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceSpans"/> class.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal whether it is start or stop.</param>
    public ResourceSpans(Activity activity, TracesData.Signal signal)
    {
        this.ScopeSpans.Add(new ScopeSpans(activity, signal));
    }

    /// <summary>
    /// Gets the resource attributes.
    /// </summary>
    public Collection<ScopeSpans> ScopeSpans { get; } = [];
}
