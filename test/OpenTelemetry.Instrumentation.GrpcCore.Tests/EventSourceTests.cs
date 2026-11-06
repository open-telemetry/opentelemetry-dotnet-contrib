// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.GrpcCore.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_GrpcCoreInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<GrpcCoreInstrumentationEventSource>();

    [Fact]
    public void EventSourceTests_GrpcCoreInstrumentationEventSource_WritesEvents() =>
        EventSourceTestHelper.ExerciseEventSource<GrpcCoreInstrumentationEventSource>();
}
