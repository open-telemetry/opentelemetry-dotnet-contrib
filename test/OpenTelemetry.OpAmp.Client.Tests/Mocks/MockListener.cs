// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal class MockListener : IOpAmpListener<CustomMessageMessage>, IDisposable
{
    private readonly AutoResetEvent messageEvent = new(false);
    private readonly Lock syncRoot = new();

    private int messageCount;
    private int waitTarget = int.MaxValue;
    private TaskCompletionSource<bool> messagesReached = CreateCompletionSource();

    public ConcurrentBag<CustomMessageMessage> Messages { get; private set; } = [];

    public void HandleMessage(CustomMessageMessage message)
    {
        this.Messages.Add(message);

        lock (this.syncRoot)
        {
            if (++this.messageCount >= this.waitTarget)
            {
                this.messagesReached.TrySetResult(true);
            }
        }

        this.messageEvent.Set();
    }

    public bool TryWaitForMessage(TimeSpan timeout)
    {
        return this.messageEvent.WaitOne(timeout);
    }

    public void WaitForMessages(TimeSpan timeout)
    {
        if (!this.TryWaitForMessage(timeout))
        {
            throw new TimeoutException($"No message was received within {timeout}.");
        }
    }

    public Task WaitForMessagesAsync(int count)
        => this.WaitForMessagesAsync(count, TimeSpan.FromSeconds(5));

    public async Task WaitForMessagesAsync(int count, TimeSpan timeout)
    {
        if (!await this.TryWaitForMessagesAsync(count, timeout).ConfigureAwait(false))
        {
            throw new TimeoutException($"Fewer than {count} message(s) were received within {timeout}.");
        }
    }

    public Task<bool> TryWaitForMessagesAsync(int count, TimeSpan timeout)
    {
        Task task;

        lock (this.syncRoot)
        {
            if (this.messageCount >= count)
            {
                return Task.FromResult(true);
            }

            this.waitTarget = count;
            this.messagesReached = CreateCompletionSource();
            task = this.messagesReached.Task;
        }

        return WaitForTaskAsync(task, timeout);
    }

    public void Dispose()
    {
        this.messageEvent.Dispose();
    }

    private static async Task<bool> WaitForTaskAsync(Task task, TimeSpan timeout)
    {
        using var timeoutCancellation = new CancellationTokenSource();
        var timeoutTask = Task.Delay(timeout, timeoutCancellation.Token);

        var completed = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false) == task;

        timeoutCancellation.Cancel();
        return completed;
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
