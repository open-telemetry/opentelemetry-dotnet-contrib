// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNet.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNet.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_AspNetInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<AspNetInstrumentationEventSource>();
}
