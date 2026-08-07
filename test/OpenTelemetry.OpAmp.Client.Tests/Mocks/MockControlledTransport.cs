// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Transport;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal sealed class MockControlledTransport : IOpAmpTransport, IDisposable
{
    private readonly ConcurrentQueue<AgentToServer> messages = [];
    private readonly ConcurrentQueue<TaskCompletionSource<bool>> sendCompletions = [];
    private readonly object syncRoot = new();
    private Action? firstSendCallback;

    private int waitTarget;
    private TaskCompletionSource<bool> messagesReached = CreateCompletionSource();

    public MockControlledTransport(Action? firstSendCallback = null)
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
                this.messagesReached.TrySetResult(true);
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
            return WaitForTaskAsync(this.messagesReached.Task, timeout);
        }
    }

    public void CompleteNextSend(Action? beforeCompletion = null)
    {
        if (!this.sendCompletions.TryDequeue(out var sendCompletion))
        {
            throw new InvalidOperationException("No send is waiting to complete.");
        }

        beforeCompletion?.Invoke();
        sendCompletion.SetResult(true);
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

    private static async Task WaitForTaskAsync(Task task, TimeSpan timeout)
    {
        using var timeoutCancellation = new CancellationTokenSource();
        var timeoutTask = Task.Delay(timeout, timeoutCancellation.Token);

        if (await Task.WhenAny(task, timeoutTask).ConfigureAwait(false) != task)
        {
            throw new TimeoutException("Timed out waiting for messages.");
        }

        timeoutCancellation.Cancel();
        await task.ConfigureAwait(false);
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
