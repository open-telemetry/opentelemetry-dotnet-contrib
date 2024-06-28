// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Stackdriver.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.Stackdriver.Tests;

public class ActivityExtensionsTest
{
    static ActivityExtensionsTest()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(listener);
    }

    [Fact]
    public void ActivityExtensions_Export_Duplicate_Activity_Tag_Key()
    {
        var activity = new Activity("Test");
        activity.SetStartTime(activity.StartTimeUtc.ToUniversalTime());

        activity.AddTag("key", "value");
        activity.AddTag("key2", "value2");
        activity.AddTag("key2", "value3");

        var span = activity.ToSpan("project1");
        Assert.True(span.Attributes.AttributeMap.Count == 2);
        Assert.True(span.Attributes.AttributeMap["key2"].StringValue.Value == "value3");
    }

    [Fact]
    public void ActivityExtensions_Export_Duplicate_ActivityLink_Tag_Key()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId1 = ActivitySpanId.CreateRandom();
        var spanId2 = ActivitySpanId.CreateRandom();

        ActivityContext context1 = new ActivityContext(traceId, spanId1, ActivityTraceFlags.Recorded, isRemote: true);
        ActivityContext context2 = new ActivityContext(traceId, spanId2, ActivityTraceFlags.Recorded, isRemote: true);

        KeyValuePair<string, object?>[] dupTags = new[]
        {
            new KeyValuePair<string, object?>("key1", "value1"),
            new KeyValuePair<string, object?>("key2", "value2"),
            new KeyValuePair<string, object?>("key1", "value3"),
        };
        ActivityLink link1 = new ActivityLink(context1, tags: new ActivityTagsCollection(dupTags));
        ActivityLink link2 = new ActivityLink(context2);

        var links = new List<ActivityLink> { link1, link2 };

        using var source = new ActivitySource("TestActivitySource");
        using var activity = source.StartActivity("NewActivityWithLinks", ActivityKind.Internal, parentContext: default, links: links);

        var span = activity?.ToSpan("project1");
        Assert.True((span?.Links.Link.Count ?? 0) == 2);
        Assert.True(span?.Links.Link.First().Attributes.AttributeMap["key1"].StringValue.Value == "value3");
    }
}
