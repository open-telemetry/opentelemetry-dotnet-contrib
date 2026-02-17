// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Quartz.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Quartz.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_QuartzInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<QuartzInstrumentationEventSource>();
}
