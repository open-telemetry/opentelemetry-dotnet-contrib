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
        await this.ForceFlushAsync().ConfigureAwait(false);

        AgentToServer message;

        lock (this.frameLock)
        {
            this.isBusy = true;
            this.hasAccumulatedData = false;

            this.currentFrame.Clear();
            MessageBuilderHelper.AppendAgentDisconnect(this.currentFrame);
            message = this.currentFrame.Build();
        }

        try
        {
            OpAmpClientEventSource.Log.SendingMessage();

            await this.transport.SendAsync(message, token)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            OpAmpClientEventSource.Log.SendMessageException(ex);
            throw;
        }

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

    public async Task ForceFlushAsync()
    {
        Task? taskToAwait;

        lock (this.frameLock)
        {
            taskToAwait = this.flushTask;
        }

        if (taskToAwait != null)
        {
            await taskToAwait.ConfigureAwait(false);
        }

        lock (this.frameLock)
        {
            taskToAwait = this.TryStartFlushLocked(force: true) ?? this.flushTask;
        }

        if (taskToAwait != null)
        {
            await taskToAwait.ConfigureAwait(false);
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
        }

        this.tokenSource.Cancel();
        this.tokenSource.Dispose();

        if (this.transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    internal void TryFlush(bool force = false)
    {
        lock (this.frameLock)
        {
            this.TryStartFlushLocked(force);
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

    private Task? TryStartFlushLocked(bool force = false)
    {
        if (this.isDisposed || !this.hasAccumulatedData)
        {
            return null;
        }

        if (this.isBusy && !force)
        {
            return null;
        }

        this.isBusy = true;

        var message = this.currentFrame.Build();
        this.hasAccumulatedData = false;

        this.flushTask = this.SendMessageAsync(message);

        return this.flushTask;
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
