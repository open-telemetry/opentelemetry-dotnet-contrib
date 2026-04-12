// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Geneva.Transports;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Geneva.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_EtwEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<EtwDataTransport.EtwEventSource>();

    [Fact]
    public void EventSourceTests_ExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<ExporterEventSource>();

    [Fact]
    public void EventSourceTests_MetricWindowsEventTracingDataTransport() =>
        EventSourceTestHelper.ValidateEventSourceIds<MetricWindowsEventTracingDataTransport>();
}
