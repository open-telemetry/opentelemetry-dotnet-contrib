// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Resources.OperatingSystem.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OperatingSystemResourcesEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<OperatingSystemResourcesEventSource>();
}
