// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Instana.Implementation;
using OpenTelemetry.Exporter.Instana.Implementation.Processors;
using Xunit;

namespace OpenTelemetry.Exporter.Instana.Tests.Processors;

public class EventsActivityProcessorTests
{
    private EventsActivityProcessor eventsActivityProcessor = new EventsActivityProcessor();

    [Fact]
    public async Task ProcessAsync()
    {
        var activityTagsCollection = new ActivityTagsCollection();
        activityTagsCollection.Add(new KeyValuePair<string, object?>("eventTagKey", "eventTagValue"));
        var activityEvent = new ActivityEvent(
            "testActivityEvent",
            DateTimeOffset.MinValue,
            activityTagsCollection);

        var activityTagsCollection2 = new ActivityTagsCollection();
        activityTagsCollection2.Add(new KeyValuePair<string, object?>("eventTagKey2", "eventTagValue2"));
        var activityEvent2 = new ActivityEvent(
            "testActivityEvent2",
            DateTimeOffset.MaxValue,
            activityTagsCollection2);

        Activity activity = new Activity("testOperationName");
        activity.AddEvent(activityEvent);
        activity.AddEvent(activityEvent2);
        InstanaSpan instanaSpan = new InstanaSpan() { TransformInfo = new Implementation.InstanaSpanTransformInfo() };
        await this.eventsActivityProcessor.ProcessAsync(activity, instanaSpan);

        Assert.True(instanaSpan.Ec == 0);
        Assert.True(instanaSpan.Data.Events.Count == 2);
        Assert.True(instanaSpan.Data.Events[0].Name == "testActivityEvent");
        Assert.True(instanaSpan.Data.Events[0].Ts > 0);
        Assert.True(instanaSpan.Data.Events[0].Tags["eventTagKey"] == "eventTagValue");
        Assert.True(instanaSpan.Data.Events[1].Name == "testActivityEvent2");
        Assert.True(instanaSpan.Data.Events[1].Ts > 0);
        Assert.True(instanaSpan.Data.Events[1].Tags["eventTagKey2"] == "eventTagValue2");
    }
}
