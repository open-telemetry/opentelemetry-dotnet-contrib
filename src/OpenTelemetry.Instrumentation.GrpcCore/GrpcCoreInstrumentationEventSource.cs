// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Instrumentation.GrpcCore;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Instrumentation-GrpcCore")]
internal sealed class GrpcCoreInstrumentationEventSource : EventSource
{
    public static readonly GrpcCoreInstrumentationEventSource Log = new();

    [Event(1, Message = "The limit of {1} message events was reached for a call to '{0}'. Further message events for the call will not be recorded.", Level = EventLevel.Warning)]
    public void MessageEventLimitExceeded(string method, int limit)
    {
        this.WriteEvent(1, method, limit);
    }
}
