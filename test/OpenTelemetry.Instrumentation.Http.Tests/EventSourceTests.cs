// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_HttpInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<HttpInstrumentationEventSource>();
}
