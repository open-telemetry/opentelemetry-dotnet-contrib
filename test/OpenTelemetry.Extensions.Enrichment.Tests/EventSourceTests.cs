// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Extensions.Enrichment.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_EnrichmentEventSource()
    {
        var eventSourceType = typeof(TraceEnricher).Assembly.GetType("OpenTelemetry.Extensions.Enrichment.EnrichmentEventSource", throwOnError: true)!;
        EventSourceTestHelper.ValidateEventSourceIds(eventSourceType);
    }
}
