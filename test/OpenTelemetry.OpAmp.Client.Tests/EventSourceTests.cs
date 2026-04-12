// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpAmpClientEventSource()
        => EventSourceTestHelper.ValidateEventSourceIds<OpAmpClientEventSource>();
}
