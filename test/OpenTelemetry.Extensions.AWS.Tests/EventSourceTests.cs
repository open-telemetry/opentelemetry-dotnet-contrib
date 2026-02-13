// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Extensions.AWS.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_AWSXRayEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<AWSXRayEventSource>();
}
