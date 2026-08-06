// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class OpAmpPipe : IDisposable
{
    private readonly IOpAmpTransport transport;
    private readonly FrameProcessor processor;
    private readonly Lock frameLock = new();
    private readonly CancellationTokenSource tokenSource = new();

    private bool isDisposed;
    private bool isBusy;
    private bool hasAccumulatedData;
    private FrameBuilder currentFrame;
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

        this.processor.SubscribeToServerMessages(this.OnServerFrameReceived);
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token)
                .ConfigureAwait(false);
        }

        this.AppendMessage(MessageBuilderHelper.AppendIdentification);
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        // Drain queued data.
        await this.FlushAsync()
            .ConfigureAwait(false);

        this.AppendMessage(MessageBuilderHelper.AppendAgentDisconnect);

        // Send disconnect.
        await this.FlushAsync()
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
            messageRequest(this.currentFrame);
            this.hasAccumulatedData = true;
        }

        this.TryFlush();
    }

    public Task FlushAsync()
    {
        lock (this.frameLock)
        {
            this.TryStartFlushLocked();

            if (this.IsFlushCompleteLocked())
            {
                return Task.CompletedTask;
            }

            this.flushCompletion ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            return this.flushCompletion.Task;
        }
    }

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

        if (this.transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    internal void TryFlush()
    {
        lock (this.frameLock)
        {
            this.TryStartFlushLocked();
            this.TryCompleteFlushLocked();
        }
    }

    private static IOpAmpTransport ConstructTransport(OpAmpClientSettings settings, FrameProcessor processor)
    {
        return settings.ConnectionType switch
        {
            ConnectionType.WebSocket => new WsTransport(settings, processor),
            ConnectionType.Http => new PlainHttpTransport(settings, processor),
            _ => throw new NotSupportedException("Unsupported transport type"),
        };
    }

    private bool IsFlushCompleteLocked()
        => this.isDisposed || (!this.hasAccumulatedData && !this.isBusy);

    private Task? TryStartFlushLocked()
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

        this.flushTask = this.SendMessageAsync(message);

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

    private async Task SendMessageAsync(AgentToServer message)
    {
        try
        {
            OpAmpClientEventSource.Log.SendingMessage();

            await this.transport.SendAsync(message, this.tokenSource.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (this.frameLock)
            {
                this.isBusy = false;
            }

            OpAmpClientEventSource.Log.SendMessageException(ex);
            this.TryFlush();
        }
    }

    private void OnServerFrameReceived(ServerToAgent message)
    {
        lock (this.frameLock)
        {
            this.isBusy = false;
        }

        this.TryFlush();
    }
}
