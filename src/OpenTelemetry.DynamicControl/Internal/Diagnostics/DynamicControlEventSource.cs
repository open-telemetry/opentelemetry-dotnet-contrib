// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Diagnostics;

/// <summary>
/// The <see cref="EventSource"/> for this component's internal diagnostics.
/// </summary>
[EventSource(Name = "OpenTelemetry-DynamicControl")]
internal sealed class DynamicControlEventSource : EventSource
{
    public static readonly DynamicControlEventSource Log = new();

    private const int EventIdPolicyChangeSubscriberFailure = 1;

    /// <summary>
    /// Records that a policy change subscriber's callback threw an exception. The
    /// exception is isolated to the subscription that raised it; it does not affect the
    /// store, other subscribers, or the commit that triggered the notification.
    /// </summary>
    /// <param name="ex">The exception thrown by the subscriber callback.</param>
    [NonEvent]
    public void PolicyChangeSubscriberException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.PolicyChangeSubscriberFailure(ex.ToInvariantString());
        }
    }

    [Event(EventIdPolicyChangeSubscriberFailure, Message = "Policy change subscriber callback failed: {0}", Level = EventLevel.Warning)]
    public void PolicyChangeSubscriberFailure(string exception)
    {
        this.WriteEvent(EventIdPolicyChangeSubscriberFailure, exception);
    }
}
