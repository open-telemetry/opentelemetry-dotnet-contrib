// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_InstanaExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<InstanaExporterEventSource>();
}
