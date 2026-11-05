// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Hangfire.Implementation;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Instrumentation.Hangfire.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_HangfireInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<HangfireInstrumentationEventSource>();
}
