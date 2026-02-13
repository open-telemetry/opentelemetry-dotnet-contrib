// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Wcf.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Wcf.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_WcfInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<WcfInstrumentationEventSource>();
}
