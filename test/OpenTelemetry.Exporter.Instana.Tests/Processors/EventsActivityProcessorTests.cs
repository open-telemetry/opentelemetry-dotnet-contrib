// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors;

public class EventsActivityProcessorTests
{
    [Fact]
    public void Process_PopulatesSpan()
    {
        // Arrange
        var activityTagsCollection = new ActivityTagsCollection { new KeyValuePair<string, object?>("eventTagKey", "eventTagValue") };
        var activityEvent = new ActivityEvent(
            "testActivityEvent",
            DateTimeOffset.MinValue,
            activityTagsCollection);

        var activityTagsCollection2 = new ActivityTagsCollection { new KeyValuePair<string, object?>("eventTagKey2", "eventTagValue2") };
        var activityEvent2 = new ActivityEvent(
            "testActivityEvent2",
            DateTimeOffset.MaxValue,
            activityTagsCollection2);

        var activity = new Activity("testOperationName");
        activity.AddEvent(activityEvent);
        activity.AddEvent(activityEvent2);
        var instanaSpan = new InstanaSpan() { TransformInfo = new Implementation.InstanaSpanTransformInfo() };

        // Act
        var processor = new EventsActivityProcessor();
        processor.Process(activity, instanaSpan);

        // Assert
        Assert.NotNull(instanaSpan.Data);
        Assert.NotNull(instanaSpan.Data.Events);

        Assert.Equal(0, instanaSpan.Ec);
        Assert.Equal(2, instanaSpan.Data.Events.Count);

        Assert.Equal("testActivityEvent", instanaSpan.Data.Events[0].Name);
        Assert.True(instanaSpan.Data.Events[0].Ts > 0);
        Assert.NotNull(instanaSpan.Data.Events[0].Tags);

        Assert.True(instanaSpan.Data.Events[0].Tags.TryGetValue("eventTagKey", out var eventTagValue));
        Assert.Equal("eventTagValue", eventTagValue);

        Assert.Equal("testActivityEvent2", instanaSpan.Data.Events[1].Name);
        Assert.True(instanaSpan.Data.Events[1].Ts > 0);
        Assert.NotNull(instanaSpan.Data?.Events[1].Tags);

        Assert.True(instanaSpan.Data.Events[1].Tags.TryGetValue("eventTagKey2", out eventTagValue));
        Assert.Equal("eventTagValue2", eventTagValue);
    }
}
