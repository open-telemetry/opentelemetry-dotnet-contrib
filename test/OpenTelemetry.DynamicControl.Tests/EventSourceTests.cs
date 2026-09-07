// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Diagnostics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.DynamicControl.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_DynamicControlEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<DynamicControlEventSource>();
}
