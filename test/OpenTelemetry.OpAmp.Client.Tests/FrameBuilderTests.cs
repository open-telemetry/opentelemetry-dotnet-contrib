// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Tests.DataGenerators;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class FrameBuilderTests
{
    [Fact]
    public void FrameBuilder_InitializesCorrectly()
    {
        var frameBuilder = new FrameBuilder(new());

        var frame = frameBuilder
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
            .Build();

        var frame2 = frameBuilder
            .Build();

        var frame3 = frameBuilder
            .Build();

        Assert.Equal(1UL, frame1.SequenceNum);
        Assert.Equal(2UL, frame2.SequenceNum);
        Assert.Equal(3UL, frame3.SequenceNum);
    }

    [Fact]
    public void AddEffectiveConfig_DuplicateFileName_ThrowsArgumentException()
    {
        var frameBuilder = new FrameBuilder(new());
        var files = new[]
        {
            new EffectiveConfigFile(Array.Empty<byte>(), string.Empty, "config.yaml"),
            new EffectiveConfigFile(Array.Empty<byte>(), string.Empty, "config.yaml"),
        };

        Assert.Throws<ArgumentException>(() =>
            ((IFrameBuilder)frameBuilder).AddEffectiveConfig(files));
    }

    [Fact]
    public void FrameBuilder_Clear()
    {
        var frameBuilder = new FrameBuilder(new());

        ((IFrameBuilder)frameBuilder).AddCustomMessage("temp-message", "temp", "temp"u8.ToArray());

        frameBuilder.Clear();

        var message = frameBuilder.Build();

        Assert.Null(message.CustomMessage);
    }

    [Theory]
    [ClassData(typeof(FrameBuilderTestData))]
    internal void FrameBuilder_AddPartial(Func<IFrameBuilder, IFrameBuilder> addMessage, Func<AgentToServer, object> propertyFetcher)
    {
        var frameBuilder = new FrameBuilder(new());
        addMessage(frameBuilder);

        var message = frameBuilder.Build();
        var property = propertyFetcher(message);

        Assert.NotNull(property);
    }
}
