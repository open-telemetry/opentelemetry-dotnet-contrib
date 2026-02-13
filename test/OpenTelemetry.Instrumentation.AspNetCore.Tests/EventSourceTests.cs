// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_AspNetCoreInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<AspNetCoreInstrumentationEventSource>();
}
