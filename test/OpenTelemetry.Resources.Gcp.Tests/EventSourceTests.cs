// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Resources.Gcp.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_GcpResourcesEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<GcpResourcesEventSource>();
}
