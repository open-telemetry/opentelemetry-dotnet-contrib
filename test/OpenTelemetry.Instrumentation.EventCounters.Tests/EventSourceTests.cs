// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.EventCounters.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_EventCountersInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<EventCountersInstrumentationEventSource>();

    [Fact]
    public void EventSourceTests_EventCountersInstrumentationEventSource_WritesEvents() =>
        EventSourceTestHelper.ExerciseEventSource<EventCountersInstrumentationEventSource>();
}
