// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Resources.AWS.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_AWSResourcesEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<AWSResourcesEventSource>();
}
