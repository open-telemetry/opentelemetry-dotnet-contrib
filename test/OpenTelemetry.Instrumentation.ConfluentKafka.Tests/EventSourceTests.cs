// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_ConfluentKafkaInstrumentationEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<ConfluentKafkaInstrumentationEventSource>();
}
