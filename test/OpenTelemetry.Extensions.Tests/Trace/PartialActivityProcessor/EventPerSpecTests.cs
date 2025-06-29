// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class EventPerSpecTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        var dateTimeOffset = DateTimeOffset.UtcNow;
        var activityEvent = new ActivityEvent(
            "TestEvent",
            dateTimeOffset,
            new ActivityTagsCollection { { "key1", "value1" }, { "key2", "value2" } });

        var eventPerSpec = new EventPerSpec(activityEvent);

        var expectedTimeUnixNano = SpecHelper.ToUnixTimeNanoseconds(dateTimeOffset);
        var actualTimeUnixNano = eventPerSpec.TimeUnixNano;

        Assert.Equal(expectedTimeUnixNano, actualTimeUnixNano);
        Assert.Equal("TestEvent", eventPerSpec.Name);
        Assert.NotNull(eventPerSpec.Attributes);
        Assert.Collection(
            eventPerSpec.Attributes,
            item =>
            {
                Assert.Equal("key1", item.Key);
                Assert.Equal("value1", item.Value?.StringValue);
            },
            item =>
            {
                Assert.Equal("key2", item.Key);
                Assert.Equal("value2", item.Value?.StringValue);
            });
    }

    [Fact]
    public void Constructor_ShouldHandleEmptyTags()
    {
        var timestamp = DateTime.UtcNow;
        var activityEvent = new ActivityEvent("TestEvent", timestamp);

        var eventPerSpec = new EventPerSpec(activityEvent);

        Assert.Equal(SpecHelper.ToUnixTimeNanoseconds(timestamp), eventPerSpec.TimeUnixNano);
        Assert.Equal("TestEvent", eventPerSpec.Name);
        Assert.Empty(eventPerSpec.Attributes);
    }
}
