// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using System.Collections.Concurrent;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
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
        {
            lock (this.syncRoot)
            {
                if (this.messages.Count >= count)
                {
                    return Task.CompletedTask;
                }

                this.waitTarget = count;
                this.messagesReached = CreateCompletionSource();
                return this.messagesReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
