// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class FrameProcessorTests
{
    [Fact]
    public void FrameProcessor_Subscribe()
    {
        var processor = new FrameProcessor();
        var listener = new MockListener();
        var mockFrame = FrameGenerator.GenerateMockFrame();

        processor.Subscribe(listener);
        processor.OnServerFrame(mockFrame.Frame, (int)mockFrame.Frame.Length, verifyHeader: mockFrame.HasHeader);

        Assert.Single(listener.Messages);
        Assert.Equal(mockFrame.ExptectedContent, listener.Messages[0].CustomMessage.Data.ToStringUtf8());
    }

    [Fact]
    public void FrameProcessor_Unsubscribe()
    {
        var processor = new FrameProcessor();
        var listener = new MockListener();
        var mockFrame = FrameGenerator.GenerateMockFrame();

        processor.Subscribe(listener);
        processor.OnServerFrame(mockFrame.Frame, (int)mockFrame.Frame.Length, verifyHeader: mockFrame.HasHeader);

        Assert.Single(listener.Messages);

        processor.Unsubscribe(listener);
        processor.OnServerFrame(mockFrame.Frame, (int)mockFrame.Frame.Length, verifyHeader: mockFrame.HasHeader);
        Assert.Single(listener.Messages);
    }

    [Fact]
    public async Task FrameProcessor_ThreadSafety()
    {
        var processor = new FrameProcessor();
        var listener = new MockListener();
        var mockFrame = FrameGenerator.GenerateMockFrame();
        int iterations = 1000;
        var tasks = new List<Task>
        {
            // Task to repeatedly call OnServerFrame
            Task.Run(() =>
            {
                Parallel.For(0, iterations, i =>
                {
                    processor.OnServerFrame(mockFrame.Frame, (int)mockFrame.Frame.Length, verifyHeader: mockFrame.HasHeader);
                });
            }),

            // Task to repeatedly subscribe
            Task.Run(() =>
            {
                Parallel.For(0, iterations, i =>
                {
                    processor.Subscribe(listener);
                });
            }),

            // Task to repeatedly unsubscribe
            Task.Run(() =>
            {
                Parallel.For(0, iterations, i =>
                {
                    processor.Unsubscribe(listener);
                });
            }),
        };

        await Task.WhenAll(tasks);

        // After all operations, ensure no exceptions and listener.Messages is in a valid state
        Assert.True(listener.Messages.Count >= 0);
    }
}
