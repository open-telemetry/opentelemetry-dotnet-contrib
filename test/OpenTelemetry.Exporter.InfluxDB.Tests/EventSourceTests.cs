// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.InfluxDB.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_InfluxDBEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<InfluxDBEventSource>();
}
