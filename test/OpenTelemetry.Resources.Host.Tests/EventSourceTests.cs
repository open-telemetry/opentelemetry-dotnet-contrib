// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Resources.Host.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_HostResourceEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<HostResourceEventSource>();
}
