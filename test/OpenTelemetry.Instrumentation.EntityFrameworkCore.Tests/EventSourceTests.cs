// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.EntityFrameworkCore.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.EntityFrameworkCore.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_EntityFrameworkInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<EntityFrameworkInstrumentationEventSource>();
}
