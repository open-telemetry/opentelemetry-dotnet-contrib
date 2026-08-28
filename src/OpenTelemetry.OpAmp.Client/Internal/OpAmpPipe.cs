// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Messages;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class OpAmpPipe : IDisposable
{
    private readonly IOpAmpTransport transport;
    private readonly FrameProcessor processor;
    private readonly Lock frameLock = new();
    private readonly CancellationTokenSource tokenSource = new();
    private readonly ServerFrameHandler? frameHandler;
    private readonly FrameBuilder currentFrame;

    private bool isBusy;
    private bool isDisposed;
    private bool isStopped;
    private bool hasAccumulatedData;
    private Task? flushTask;
    private TaskCompletionSource<bool>? flushCompletion;

    public OpAmpPipe(OpAmpClientSettings settings, FrameProcessor processor)
        : this(settings, processor, ConstructTransport(settings, processor))
    {
    }

    public OpAmpPipe(OpAmpClientSettings settings, FrameProcessor processor, IOpAmpTransport transport)
    {
        this.processor = processor;
        this.transport = transport;
        this.currentFrame = new FrameBuilder(settings);

        if (transport.RequiresResponseBeforeNextSend)
        {
            this.frameHandler = new(this.OnServerFrameReceived);
            this.processor.Subscribe(this.frameHandler);
        }
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token)
                .ConfigureAwait(false);
        }

        this.AppendMessage(MessageBuilderHelper.AppendIdentification);

        // Ensure identification is sent.
        await this.FlushAsync(token)
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        // Set the pipe to stopped state. New data should not accumulate.
        lock (this.frameLock)
        {
            this.isStopped = true;
        }

        // Drain all accumulated data.
        await this.FlushAsyncCore(force: true, token)
            .ConfigureAwait(false);

        token.ThrowIfCancellationRequested();

        // Do not use AppendMessage(), it is already locked for closure and does extra flush attempt.
        MessageBuilderHelper.AppendAgentDisconnect(this.currentFrame);

        // Mark the stop frame is available.
        this.hasAccumulatedData = true;

        // Do final flush and send disconnect
        await this.FlushAsyncCore(force: true, token)
            .ConfigureAwait(false);

        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StopAsync(token)
                .ConfigureAwait(false);
        }
    }

    public void AppendMessage(Action<IFrameBuilder> messageRequest)
    {
        lock (this.frameLock)
        {
            if (this.isStopped ||
                this.isDisposed ||
                this.tokenSource.IsCancellationRequested)
            {
                return; // Discard any new messages
            }

            messageRequest(this.currentFrame);
            this.hasAccumulatedData = true;
        }

        this.TryFlush(this.tokenSource.Token);
    }

    public Task FlushAsync(CancellationToken token = default) =>
        this.FlushAsyncCore(force: false, token);

    public void Dispose()
    {
        lock (this.frameLock)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            this.TryCompleteFlushLocked();
        }

        this.tokenSource.Cancel();
        this.tokenSource.Dispose();

        if (this.frameHandler != null)
        {
            this.processor.Unsubscribe(this.frameHandler);
        }

        if (this.transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    private static IOpAmpTransport ConstructTransport(OpAmpClientSettings settings, FrameProcessor processor) => settings.ConnectionType switch
    {
        ConnectionType.WebSocket => new WsTransport(settings, processor),
        ConnectionType.Http => new PlainHttpTransport(settings, processor),
        _ => throw new NotSupportedException("Unsupported transport type"),
    };

    private static async Task WaitForFlushAsync(Task flushTask, CancellationToken token)
    {
        if (flushTask.IsCompleted || !token.CanBeCanceled)
        {
            await flushTask.ConfigureAwait(false);
            return;
        }

        var cancellationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            cancellationCompletion);

        if (await Task.WhenAny(flushTask, cancellationCompletion.Task).ConfigureAwait(false) == cancellationCompletion.Task)
        {
            token.ThrowIfCancellationRequested();
        }

        await flushTask.ConfigureAwait(false);
    }

    private Task FlushAsyncCore(bool force, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        lock (this.frameLock)
        {
            if (!force && (this.isStopped ||
                this.isDisposed ||
                this.tokenSource.IsCancellationRequested))
            {
                return Task.CompletedTask;
            }

            this.TryStartFlushLocked(token);

            if (this.IsFlushCompleteLocked())
            {
                return Task.CompletedTask;
            }

            this.flushCompletion ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            return WaitForFlushAsync(this.flushCompletion.Task, token);
        }
    }

    private void TryFlush(CancellationToken token)
    {
        lock (this.frameLock)
        {
            this.TryStartFlushLocked(token);
            this.TryCompleteFlushLocked();
        }
    }

    private bool IsFlushCompleteLocked()
        => this.isDisposed || (!this.hasAccumulatedData && !this.isBusy);

    private Task? TryStartFlushLocked(CancellationToken token)
    {
        if (this.isDisposed || !this.hasAccumulatedData)
        {
            return null;
        }

        if (this.isBusy)
        {
            return null;
        }

        this.isBusy = true;

        var message = this.currentFrame.Build();
        this.hasAccumulatedData = false;

        this.flushTask = this.SendMessageAsync(message, token);

        return this.flushTask;
    }

    private void TryCompleteFlushLocked()
    {
        if (this.IsFlushCompleteLocked())
        {
            this.flushCompletion?.TrySetResult(true);
            this.flushCompletion = null;
        }
    }

    private async Task SendMessageAsync(AgentToServer message, CancellationToken token)
    {
        try
        {
            OpAmpClientEventSource.Log.SendingMessage();

            await this.transport.SendAsync(message, token)
                .ConfigureAwait(false);

            if (!this.transport.RequiresResponseBeforeNextSend)
            {
                this.ReleaseBusy();
                this.TryFlush(token);
            }
        }
        catch (Exception ex)
        {
            this.ReleaseBusy();

            OpAmpClientEventSource.Log.SendMessageException(ex);
            this.TryFlush(token);
        }
    }

    private void OnServerFrameReceived(ServerToAgent message)
    {
        this.ReleaseBusy();
        this.TryFlush(this.tokenSource.Token);
    }

    private void ReleaseBusy()
    {
        lock (this.frameLock)
        {
            this.isBusy = false;
        }
    }

    private sealed class ServerFrameHandler : IOpAmpListener<ServerToAgentMessage>
    {
        public ServerFrameHandler(Action<ServerToAgent> callback)
        {
            this.Callback = callback;
        }

        public Action<ServerToAgent> Callback { get; }

        public void HandleMessage(ServerToAgentMessage message) => this.Callback(message.Message);
    }
}
