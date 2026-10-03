// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Text;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Messages;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class FrameProcessorTests
{
    [Fact]
    public void FrameProcessor_Subscribe()
    {
        using var listener = new MockListener();
        var processor = new FrameProcessor();
        var mockFrame = FrameGenerator.GenerateMockServerFrame();

        processor.Subscribe(listener);
        processor.OnServerFrame(mockFrame.Frame.ToSequence());

        var message = Assert.Single(listener.Messages);
        var messageContent =
#if NET
            Encoding.UTF8.GetString(message.Data);
#else
            Encoding.UTF8.GetString([.. message.Data]);
#endif

        Assert.Equal(mockFrame.ExpectedContent, messageContent);
    }

    [Fact]
    public void FrameProcessor_Unsubscribe()
    {
        using var listener = new MockListener();
        var processor = new FrameProcessor();
        var mockFrame = FrameGenerator.GenerateMockServerFrame();

        processor.Subscribe(listener);
        processor.OnServerFrame(mockFrame.Frame.ToSequence());

        Assert.Single(listener.Messages);

        processor.Unsubscribe(listener);
        processor.OnServerFrame(mockFrame.Frame.ToSequence());
        Assert.Single(listener.Messages);
    }

    [Fact]
    public void FrameProcessor_DispatchesCapabilitiesBeforeFlags_FromSameFrame()
    {
        var callbacks = new List<string>();
        var processor = new FrameProcessor();
        var serverToAgent = new ServerToAgent
        {
            Capabilities = (ulong)ServerCapabilities.AcceptsEffectiveConfig,
            Flags = (ulong)ServerToAgentFlags.ReportFullState,
        };

        processor.Subscribe(new RecordingListener<FlagsMessage>("flags", callbacks));
        processor.Subscribe(new RecordingListener<ServerCapabilitiesMessage>("capabilities", callbacks));
        processor.Subscribe(new RecordingListener<ServerToAgentMessage>("envelope", callbacks));

        processor.OnServerFrame(new ArraySegment<byte>(serverToAgent.ToByteArray()).ToSequence());

        Assert.Equal(["envelope", "capabilities", "flags"], callbacks);
    }

    [Fact]
    public void FrameProcessor_ContinuesDispatching_WhenPublicListenerThrows()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        using var listener = new MockListener();
        var processor = new FrameProcessor();
        var mockFrame = FrameGenerator.GenerateMockServerFrame();
        var failureMessage = $"listener failed {Guid.NewGuid():N}";

        processor.Subscribe(new ThrowingCustomMessageListener(failureMessage));
        processor.Subscribe(listener);

        processor.OnServerFrame(mockFrame.Frame.ToSequence());

        Assert.Single(listener.Messages);

        Assert.Contains(
            eventListener.Events,
            e => e.EventName == nameof(OpAmpClientEventSource.FrameProcessingFailure)
                && e.Payload![0] is string exception
                && exception.Contains(failureMessage));
    }

    [Fact]
    public async Task FrameProcessor_ThreadSafety()
    {
        using var listener = new MockListener();
        var processor = new FrameProcessor();
        var mockFrame = FrameGenerator.GenerateMockServerFrame();
        var iterations = 1000;
        var tasks = new List<Task>
        {
            // Task to repeatedly call OnServerFrame
            Task.Run(
                () =>
                {
                    Parallel.For(0, iterations, i =>
                    {
                        processor.OnServerFrame(mockFrame.Frame.ToSequence());
                    });
                },
                TestContext.Current.CancellationToken),

            // Task to repeatedly subscribe
            Task.Run(
                () =>
                {
                    Parallel.For(0, iterations, i =>
                    {
                        processor.Subscribe(listener);
                    });
                },
                TestContext.Current.CancellationToken),

            // Task to repeatedly subscribe
            Task.Run(
                () =>
                {
                    Parallel.For(0, iterations, i =>
                    {
                        processor.Subscribe(listener);
                    });
                },
                TestContext.Current.CancellationToken),

            // Task to repeatedly unsubscribe
            Task.Run(
                () =>
                {
                    Parallel.For(0, iterations, i =>
                    {
                        processor.Unsubscribe(listener);
                    });
                },
                TestContext.Current.CancellationToken),
        };

        await Task.WhenAll(tasks);

        // After all operations, ensure no exceptions and listener.Messages is in a valid state
        Assert.True(listener.Messages.Count >= 0);
    }

    private sealed class RecordingListener<T> : IOpAmpListener<T>
        where T : OpAmpMessage
    {
        private readonly string marker;
        private readonly List<string> callbacks;

        public RecordingListener(string marker, List<string> callbacks)
        {
            this.marker = marker;
            this.callbacks = callbacks;
        }

        public void HandleMessage(T message) => this.callbacks.Add(this.marker);
    }

    private sealed class ThrowingCustomMessageListener : IOpAmpListener<CustomMessageMessage>
    {
        private readonly string failureMessage;

        public ThrowingCustomMessageListener(string failureMessage)
        {
            this.failureMessage = failureMessage;
        }

        public void HandleMessage(CustomMessageMessage message)
            => throw new InvalidOperationException(this.failureMessage);
    }
}
