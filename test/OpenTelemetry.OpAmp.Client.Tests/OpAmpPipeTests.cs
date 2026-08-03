// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using System.Collections.Concurrent;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Settings;

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

        pipe.AppendMessage(fb => fb.AddAgentDescription());
        await transport.WaitForMessagesAsync(1);

        await Parallel.ForEachAsync(Enumerable.Range(0, taskCount), (_, _) =>
        {
            pipe.AppendMessage(fb => fb.AddAgentDescription());
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

        pipe.AppendMessage(fb => fb.AddAgentDescription());
        await transport.WaitForMessagesAsync(1);

        pipe.AppendMessage(fb => fb.AddHealth(CreateHealthReport()));
        transport.FaultNextSend(new InvalidOperationException("send failed"));

        await transport.WaitForMessagesAsync(2);
        transport.CompleteNextSend();

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL], messages.Select(m => m.SequenceNum).ToArray());
        Assert.NotNull(messages[0].AgentDescription);
        Assert.NotNull(messages[1].Health);
    }

    [Fact]
    public async Task OpAmpPipe_StopAsyncWaitsForAccumulatedDataBeforeSendingDisconnect()
    {
        using var transport = new ControlledTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        pipe.AppendMessage(fb => fb.AddAgentDescription());
        await transport.WaitForMessagesAsync(1);

        pipe.AppendMessage(fb => fb.AddHealth(CreateHealthReport()));
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

    private static HealthReport CreateHealthReport() => new()
    {
        StartTime = 1,
        StatusTime = 2,
        IsHealthy = true,
        Status = "OK",
    };

    private sealed class ControlledTransport : IOpAmpTransport, IDisposable
    {
        private readonly ConcurrentQueue<AgentToServer> messages = [];
        private readonly ConcurrentQueue<TaskCompletionSource> sendCompletions = [];
        private readonly Lock syncRoot = new();

        private int waitTarget;
        private TaskCompletionSource messagesReached = CreateCompletionSource();

        public IReadOnlyCollection<AgentToServer> Messages => this.messages.ToList().AsReadOnly();

        public Task SendAsync<T>(T message, CancellationToken token)
            where T : IMessage<T>
        {
            if (message is not AgentToServer agentToServer)
            {
                throw new InvalidOperationException("Unsupported message type. Only AgentToServer messages are supported.");
            }

            var sendCompletion = CreateCompletionSource();

            lock (this.syncRoot)
            {
                this.messages.Enqueue(agentToServer);
                this.sendCompletions.Enqueue(sendCompletion);

                if (this.messages.Count >= this.waitTarget)
                {
                    this.messagesReached.TrySetResult();
                }
            }

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
