// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.ElasticsearchClient.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.ElasticsearchClient.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_ElasticsearchInstrumentationEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<ElasticsearchInstrumentationEventSource>();
}
