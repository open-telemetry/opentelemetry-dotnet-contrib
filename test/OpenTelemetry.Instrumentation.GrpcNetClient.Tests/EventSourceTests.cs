// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_GrpcInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<GrpcInstrumentationEventSource>();
}
