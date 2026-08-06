// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpPipeTests
{
    [Fact]
    public async Task OpAmpPipe_IsThreadSafe_WhenAppendingConcurrently()
    {
        var taskCount = 20;

        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        await Parallel.ForEachAsync(Enumerable.Range(0, taskCount), (_, _) =>
        {
            AppendIdentification(pipe);
            return ValueTask.CompletedTask;
        });

        Assert.Single(transport.Messages);

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await transport.WaitForMessagesAsync(2);
        transport.CompleteNextSend();

        var messages = transport.Messages;
        var sequenceNumbers = messages
            .Select(m => m.SequenceNum)
            .ToArray();

        Assert.Equal([1UL, 2UL], sequenceNumbers);
        Assert.All(messages, message => Assert.NotNull(message.AgentDescription));
    }

    [Fact]
    public async Task OpAmpPipe_ForceFlushCompletes_WhenNoMessagesArePending()
    {
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        await pipe.ForceFlushAsync();

        Assert.Empty(transport.Messages);
    }

    [Fact]
    public async Task OpAmpPipe_ForceFlushIsThreadSafe_WhenCalledConcurrentlyWithoutPendingMessages()
    {
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        await Parallel.ForEachAsync(Enumerable.Range(0, 20), async (_, _) =>
        {
            await pipe.ForceFlushAsync();
        });

        Assert.Empty(transport.Messages);
    }

    [Fact]
    public async Task OpAmpPipe_FlushesAccumulatedData_WhenCurrentSendFails()
    {
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        AppendHeartbeat(pipe);
        transport.FaultNextSend(new InvalidOperationException("send failed"));

        await transport.WaitForMessagesAsync(2);
        transport.CompleteNextSend();

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL], messages.Select(m => m.SequenceNum).ToArray());
        Assert.NotNull(messages[0].AgentDescription);
        Assert.NotNull(messages[1].Health);
    }

    [Fact]
    public async Task OpAmpPipe_LogsSendFailure_WhenCurrentSendFails()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendHeartbeat(pipe);
        await transport.WaitForMessagesAsync(1);
        transport.FaultNextSend(new InvalidOperationException("send failed"));

        var failedEvent = await WaitForEventAsync(
            eventListener,
            nameof(OpAmpClientEventSource.FailedToSendMessage),
            TimeSpan.FromSeconds(5));

        Assert.Contains("send failed", Assert.IsType<string>(failedEvent.Payload![0]));
    }

    [Fact]
    public async Task OpAmpPipe_DoesNotReleaseNewerSend_WhenPublicListenerThrowsDuringHttpResponseProcessing()
    {
        var processor = new FrameProcessor();
        var serverFrame = new ServerToAgent
        {
            CustomMessage = new CustomMessage
            {
                Data = ByteString.CopyFromUtf8("response"),
                Type = "Utf8String",
            },
        }.ToByteArray();

        using var transport = new ControlledTransport(
            () => processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame)));
        var settings = new OpAmpClientSettings();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        processor.Subscribe(new AppendHeartbeatAndThrowListener(pipe));

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(2);

        AppendHeartbeat(pipe);

        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.WaitForMessagesAsync(3, TimeSpan.FromMilliseconds(250)));

        Assert.Equal(2, transport.Messages.Count);
    }

    [Fact]
    public async Task OpAmpPipe_LogsQueueAction_WhenMessageIsAppended()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendHeartbeat(pipe);

        await WaitForEventAsync(
            eventListener,
            nameof(OpAmpClientEventSource.QueueingHeartbeatMessage),
            TimeSpan.FromSeconds(5));

        await transport.WaitForMessagesAsync(1);
        transport.CompleteNextSend();
    }

    [Fact]
    public async Task OpAmpPipe_StopAsyncWaitsForAccumulatedDataBeforeSendingDisconnect()
    {
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        AppendHeartbeat(pipe);
        var stopTask = pipe.StopAsync();

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await transport.WaitForMessagesAsync(2);
        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.WaitForMessagesAsync(3, TimeSpan.FromMilliseconds(250)));

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await transport.WaitForMessagesAsync(3);
        transport.CompleteNextSend();
        await stopTask;

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL, 3UL], messages.Select(m => m.SequenceNum).ToArray());
        Assert.NotNull(messages[0].AgentDescription);
        Assert.NotNull(messages[1].Health);
        Assert.NotNull(messages[2].AgentDisconnect);
    }

    private static void AppendIdentification(OpAmpPipe pipe)
        => pipe.AppendMessage(MessageBuilderHelper.AppendIdentification);

    private static void AppendHeartbeat(OpAmpPipe pipe)
        => pipe.AppendMessage(MessageBuilderHelper.AppendHeartbeat(CreateHealthReport()));

    private static async Task<EventWrittenEventArgs> WaitForEventAsync(InMemoryEventListener eventListener, string eventName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            while (eventListener.Events.TryDequeue(out var candidate))
            {
                if (candidate.EventName == eventName)
                {
                    return candidate;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for event '{eventName}'.");
    }

    private static HealthReport CreateHealthReport() => new()
    {
        StartTime = 1,
        StatusTime = 2,
        IsHealthy = true,
        Status = "OK",
    };

    private sealed class AppendHeartbeatAndThrowListener : IOpAmpListener<CustomMessageMessage>
    {
        private readonly OpAmpPipe pipe;

        public AppendHeartbeatAndThrowListener(OpAmpPipe pipe)
        {
            this.pipe = pipe;
        }

        public void HandleMessage(CustomMessageMessage message)
        {
            AppendHeartbeat(this.pipe);
            throw new InvalidOperationException("listener failed");
        }
    }

    private sealed class ControlledTransport : IOpAmpTransport, IDisposable
    {
        private readonly ConcurrentQueue<AgentToServer> messages = [];
        private readonly ConcurrentQueue<TaskCompletionSource> sendCompletions = [];
        private readonly Lock syncRoot = new();
        private Action? firstSendCallback;

        private int waitTarget;
        private TaskCompletionSource messagesReached = CreateCompletionSource();

        public ControlledTransport(Action? firstSendCallback = null)
        {
            this.firstSendCallback = firstSendCallback;
        }

        public ReadOnlyCollection<AgentToServer> Messages => this.messages.ToList().AsReadOnly();

        public Task SendAsync<T>(T message, CancellationToken token)
            where T : IMessage<T>
        {
            if (message is not AgentToServer agentToServer)
            {
                throw new InvalidOperationException("Unsupported message type. Only AgentToServer messages are supported.");
            }

            var sendCompletion = CreateCompletionSource();
            Action? firstSendCallback;

            lock (this.syncRoot)
            {
                this.messages.Enqueue(agentToServer);
                this.sendCompletions.Enqueue(sendCompletion);
                firstSendCallback = this.firstSendCallback;
                this.firstSendCallback = null;

                if (this.messages.Count >= this.waitTarget)
                {
                    this.messagesReached.TrySetResult();
                }
            }

            firstSendCallback?.Invoke();

            return sendCompletion.Task;
        }

        public Task WaitForMessagesAsync(int count)
            => this.WaitForMessagesAsync(count, TimeSpan.FromSeconds(5));

        public Task WaitForMessagesAsync(int count, TimeSpan timeout)
        {
            lock (this.syncRoot)
            {
                if (this.messages.Count >= count)
                {
                    return Task.CompletedTask;
                }

                this.waitTarget = count;
                this.messagesReached = CreateCompletionSource();
                return this.messagesReached.Task.WaitAsync(timeout);
            }
        }

        public void CompleteNextSend()
        {
            if (!this.sendCompletions.TryDequeue(out var sendCompletion))
            {
                throw new InvalidOperationException("No send is waiting to complete.");
            }

            sendCompletion.SetResult();
        }

        public void FaultNextSend(Exception exception)
        {
            if (!this.sendCompletions.TryDequeue(out var sendCompletion))
            {
                throw new InvalidOperationException("No send is waiting to complete.");
            }

            sendCompletion.SetException(exception);
        }

        public void Dispose()
        {
            while (this.sendCompletions.TryDequeue(out var sendCompletion))
            {
                sendCompletion.TrySetCanceled();
            }
        }

        private static TaskCompletionSource CreateCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
#endif
