// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OneCollector.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OneCollectorExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<OneCollectorExporterEventSource>();
}
