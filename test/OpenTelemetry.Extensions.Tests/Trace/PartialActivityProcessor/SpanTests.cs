// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Extensions.Trace.PartialActivityProcessor;
using Xunit;

namespace OpenTelemetry.Extensions.Tests.Trace.PartialActivityProcessor;

public class SpanTests
{
    [Fact]
    public void Constructor_ShouldMapActivityPropertiesCorrectly()
    {
        var activity = new Activity("TestActivity");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        activity.Stop();

        var span = new Span(activity, TracesData.Signal.Stop);

        Assert.Equal(activity.TraceId.ToHexString(), span.TraceId);
        Assert.Equal(activity.SpanId.ToHexString(), span.SpanId);
        Assert.Equal(activity.Status.ToString(), span.TraceState);
        Assert.Equal(activity.ParentSpanId.ToHexString(), span.ParentSpanId);
        Assert.Equal((uint)activity.ActivityTraceFlags, span.Flags);
        Assert.Equal(activity.DisplayName, span.Name);
        Assert.Equal(SpanKind.Internal, span.Kind);
        Assert.Equal(
            SpecHelper.ToUnixTimeNanoseconds(activity.StartTimeUtc),
            span.StartTimeUnixNano);
        Assert.Equal(
            SpecHelper.ToUnixTimeNanoseconds(activity.StartTimeUtc.Add(activity.Duration)),
            span.EndTimeUnixNano);
    }

    [Fact]
    public void Constructor_ShouldMapAttributesCorrectly()
    {
        var activity = new Activity("TestActivity");
        activity.AddTag("key1", "value1");
        activity.AddTag("key2", 123);
        activity.Start();
        activity.Stop();

        var span = new Span(activity, TracesData.Signal.Stop);

        Assert.NotNull(span.Attributes);
        Assert.Equal(2, span.Attributes.Count);
        Assert.Contains(
            span.Attributes,
            attr => attr is { Key: "key1", Value.StringValue: "value1" });
        Assert.Contains(
            span.Attributes,
            attr => attr is { Key: "key2", Value.StringValue: "123" });
    }
}
