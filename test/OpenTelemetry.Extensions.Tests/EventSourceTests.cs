// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Extensions.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpenTelemetryExtensionsEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryExtensionsEventSource>();
}
