// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.AzureDataExplorer.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_AzureDataExplorerExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<AzureDataExplorerExporterEventSource>();
}
