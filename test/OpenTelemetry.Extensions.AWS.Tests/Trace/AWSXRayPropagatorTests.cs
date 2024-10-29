// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.AWS.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.AWS.Tests;

public class AWSXRayPropagatorTests
{
    private const string AWSXRayTraceHeaderKey = "X-Amzn-Trace-Id";
    private const string TraceId = "5759e988bd862e3fe1be46a994272793";
    private const string ParentId = "53995c3f42cd8ad8";

    private static readonly string[] Empty = [];

    private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
    {
        if (headers.TryGetValue(name, out var value))
        {
            return [value];
        }

        return Empty;
    };

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    private readonly AWSXRayPropagator awsXRayPropagator = new AWSXRayPropagator();

    [Fact]
    public void TestInjectTraceHeader()
    {
        var carrier = new Dictionary<string, string>();
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.Recorded;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags);
        this.awsXRayPropagator.Inject(new PropagationContext(activityContext, default), carrier, Setter);

        Assert.True(carrier.ContainsKey(AWSXRayTraceHeaderKey));
        Assert.Equal("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1", carrier[AWSXRayTraceHeaderKey]);
    }

    [Fact]
    public void TestInjectTraceHeaderNotSampled()
    {
        var carrier = new Dictionary<string, string>();
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.None;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags);
        this.awsXRayPropagator.Inject(new PropagationContext(activityContext, default), carrier, Setter);

        Assert.True(carrier.ContainsKey(AWSXRayTraceHeaderKey));
        Assert.Equal("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=0", carrier[AWSXRayTraceHeaderKey]);
    }

    [Fact]
    public void TestExtractTraceHeader()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1" },
        };
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.Recorded;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, isRemote: true);

        Assert.Equal(new PropagationContext(activityContext, default), this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderNotSampled()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=0" },
        };
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.None;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, isRemote: true);

        Assert.Equal(new PropagationContext(activityContext, default), this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderDifferentOrder()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Sampled=1;Parent=53995c3f42cd8ad8;Root=1-5759e988-bd862e3fe1be46a994272793" },
        };
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.Recorded;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, isRemote: true);

        Assert.Equal(new PropagationContext(activityContext, default), this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithEmptyValue()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, string.Empty },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithoutParentId()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Sampled=1" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithoutSampleDecision()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithInvalidTraceId()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=15759e988bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithInvalidParentId()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=123;Sampled=1" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithInvalidSampleDecision()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithInvalidSampleDecisionValue()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=3" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithInvalidSampleDecisionLength()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=123" },
        };

        Assert.Equal(default, this.awsXRayPropagator.Extract(default, carrier, Getter));
    }

    [Fact]
    public void TestExtractTraceHeaderWithAdditionalField()
    {
        var carrier = new Dictionary<string, string>()
        {
            { AWSXRayTraceHeaderKey, "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1;Foo=Bar" },
        };
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var parentId = ActivitySpanId.CreateFromString(ParentId.AsSpan());
        var traceFlags = ActivityTraceFlags.Recorded;
        var activityContext = new ActivityContext(traceId, parentId, traceFlags, isRemote: true);

        Assert.Equal(new PropagationContext(activityContext, default), this.awsXRayPropagator.Extract(default, carrier, Getter));
    }
}
