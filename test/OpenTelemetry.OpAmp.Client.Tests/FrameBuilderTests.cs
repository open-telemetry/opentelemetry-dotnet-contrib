// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Protocol;
using OpenTelemetry.OpAmp.Client.Data;
using OpenTelemetry.OpAmp.Client.Services.Internal;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class FrameBuilderTests
{
    public static IEnumerable<object[]> TestData()
    {
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddDescription()), new Func<AgentToServer, object>(m => m.AgentDescription) };

        var healthReport = new HealthReport { DetailedStatus = new HealthStatus { IsHealthy = true, Status = "OK" } };
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddHeartbeat(healthReport)), new Func<AgentToServer, object>(m => m.Health) };

        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddCapabilities()), new Func<AgentToServer, object>(m => m.Capabilities) };
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddCurrentConfig()), new Func<AgentToServer, object>(m => m.EffectiveConfig) };
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddConfigStatus()), new Func<AgentToServer, object>(m => m.RemoteConfigStatus) };
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddPackageStatus()), new Func<AgentToServer, object>(m => m.PackageStatuses) };
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.AddDisconnectRequest()), new Func<AgentToServer, object>(m => m.AgentDisconnect) };
        yield return new object[] { new Func<IFrameBuilder, IFrameBuilder>(fb => fb.SetFlags()), new Func<AgentToServer, object>(m => m.Flags) };
    }

    [Fact]
    public void FrameBuilder_InitializesCorrectly()
    {
        var frameBuilder = new FrameBuilder(new());

        var frame = frameBuilder
            .StartBaseMessage()
            .Build();

        Assert.NotNull(frame);
        Assert.NotEmpty(frame.InstanceUid);
        Assert.Equal(1UL, frame.SequenceNum);
    }

    [Fact]
    public void FrameBuilder_Sequence()
    {
        var frameBuilder = new FrameBuilder(new());

        var frame1 = frameBuilder
            .StartBaseMessage()
            .Build();

        var frame2 = frameBuilder
            .StartBaseMessage()
            .Build();

        var frame3 = frameBuilder
            .StartBaseMessage()
            .Build();

        Assert.Equal(1UL, frame1.SequenceNum);
        Assert.Equal(2UL, frame2.SequenceNum);
        Assert.Equal(3UL, frame3.SequenceNum);
    }

    [Fact]
    public void FrameBuilder_ThrowsOnDoubleStartBaseMessage()
    {
        var frameBuilder = new FrameBuilder(new());
        frameBuilder.StartBaseMessage();

        Assert.Throws<InvalidOperationException>(frameBuilder.StartBaseMessage);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    internal void FrameBuilder_AddPartial(Func<IFrameBuilder, IFrameBuilder> addMessage, Func<AgentToServer, object> propertyFetcher)
    {
        var frameBuilder = new FrameBuilder(new());
        var messageBuilder = frameBuilder.StartBaseMessage();
        addMessage(messageBuilder);

        var message = messageBuilder.Build();
        var property = propertyFetcher(message);

        Assert.NotNull(property);
    }
}
