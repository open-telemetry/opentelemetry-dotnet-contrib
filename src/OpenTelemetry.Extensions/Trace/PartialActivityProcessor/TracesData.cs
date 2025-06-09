// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OpenTelemetry.Extensions.Trace.PartialActivityProcessor;

/// <summary>
/// TracesData per spec.
/// </summary>
public class TracesData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TracesData"/> class with a single resource span.
    /// </summary>
    /// <param name="activity">Activity.</param>
    /// <param name="signal">Signal.</param>
    public TracesData(Activity activity, Signal signal)
    {
        this.ResourceSpans.Add(new ResourceSpans(activity, signal));
    }

    /// <summary>
    /// Signal enum to indicate whether the activity is starting or stopping.
    /// </summary>
    public enum Signal
    {
        /// <summary>
        /// Signal indicating the start of an activity.
        /// </summary>
        Heartbeat,

        /// <summary>
        /// Signal indicating the stop of an activity.
        /// </summary>
        Ended,
    }

    /// <summary>
    /// Gets the resource spans.
    /// </summary>
    public Collection<ResourceSpans> ResourceSpans { get; } = [];
}
