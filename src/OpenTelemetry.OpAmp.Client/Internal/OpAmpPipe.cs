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
        this.isBusy = true;

        // Wait for previous send and stop after
        if (this.flushTask != null)
        {
            await this.flushTask
                .ConfigureAwait(false);
        }

        this.currentFrame
            .Clear()
            .AddAgentDisconnect();

        var message = this.currentFrame.Build();

        await this.transport.SendAsync(message, token)
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

    public async Task ForceFlushAsync()
    {
        if (this.flushTask != null)
        {
            await this.flushTask.ConfigureAwait(false);
        }

        this.TryFlush(force: true);
        await this.flushTask!.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        this.tokenSource.Cancel();
        this.tokenSource.Dispose();

        if (this.transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    internal void TryFlush(bool force = false)
    {
        AgentToServer? message = null;

        lock (this.frameLock)
        {
            // No data, don't process
            if (!this.hasAccumulatedData)
            {
                return;
            }

            // Is busy but not forced
            if (this.isBusy && !force)
            {
                return;
            }

            this.isBusy = true;

            message = this.currentFrame.Build();
            this.hasAccumulatedData = false;

            this.flushTask = this.SendMessageAsync(message);
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

    private async Task SendMessageAsync(AgentToServer message)
    {
        try
        {
            await this.transport.SendAsync(message, this.tokenSource.Token)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            this.isBusy = false;

            // TODO: log exception
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
