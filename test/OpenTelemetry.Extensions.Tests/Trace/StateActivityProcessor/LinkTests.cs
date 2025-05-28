// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Trace.StateActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.StateActivityProcessor;

public class LinkTests
{
    [Fact]
    public void Constructor_ShouldMapActivityLinkPropertiesCorrectly()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            "trace-state"
        );

        var activityLink = new ActivityLink(activityContext);

        var link = new Link(activityLink);

        Assert.Equal(activityContext.TraceId.ToHexString(), link.TraceId);
        Assert.Equal(activityContext.SpanId.ToHexString(), link.SpanId);
        Assert.Equal(activityContext.TraceState, link.TraceState);
        Assert.Equal(1u, link.Flags);
    }

    [Fact]
    public void Constructor_ShouldMapAttributesCorrectly()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None
        );


        var tags = new List<KeyValuePair<string, object?>>
        {
            new("key1", "value1"), new("key2", 123),
        };
        ActivityTagsCollection tagsCollection = new ActivityTagsCollection(tags);

        var activityLink = new ActivityLink(activityContext, tagsCollection);

        var link = new Link(activityLink);

        Assert.NotNull(link.Attributes);
        Assert.Equal(2, link.Attributes.Count);
        Assert.Contains(
            link.Attributes,
            attr => attr is { Key: "key1", Value.StringValue: "value1" });
        Assert.Contains(
            link.Attributes,
            attr => attr is { Key: "key2", Value.StringValue: "123" });
    }

    [Fact]
    public void Constructor_ShouldHandleNullTags()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None
        );

        var activityLink = new ActivityLink(activityContext);

        var link = new Link(activityLink);

        Assert.NotNull(link.Attributes);
        Assert.Empty(link.Attributes);
    }
}
