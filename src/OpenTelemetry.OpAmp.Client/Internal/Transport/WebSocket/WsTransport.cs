// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;

/// <summary>
/// Represents an <see cref="IOpAmpTransport"/> implementation that exchanges OpAMP frames over a WebSocket connection.
/// </summary>
internal sealed class WsTransport : IOpAmpTransport, IDisposable
{
    private readonly Uri uri;
    private readonly ClientWebSocket webSocket;
    private readonly WsReceiver receiver;
    private readonly WsTransmitter transmitter;
    private int startState;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WsTransport"/> class.
    /// </summary>
    /// <param name="settings">The client settings containing the target endpoint and WebSocket factory.</param>
    /// <param name="processor">The frame processor used to handle server frames received on the connection.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="settings"/> or <paramref name="processor"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="OpAmpClientSettings.ClientWebSocketFactory"/> returns <see langword="null"/>.
    /// </exception>
    public WsTransport(OpAmpClientSettings settings, FrameProcessor processor)
    {
        Guard.ThrowIfNull(settings, nameof(settings));
        Guard.ThrowIfNull(processor, nameof(processor));

        var webSocket = settings.ClientWebSocketFactory()
            ?? throw new InvalidOperationException("ClientWebSocketFactory returned null. The factory must return a new, unconnected ClientWebSocket instance.");

        this.uri = settings.ServerUrl;
        this.webSocket = webSocket;
        this.receiver = new WsReceiver(this.webSocket, processor);
        this.transmitter = new WsTransmitter(this.webSocket);
    }

    /// <summary>
    /// Connects the WebSocket and starts receiving server frames.
    /// </summary>
    /// <param name="token">A cancellation token for the connect operation.</param>
    /// <returns>A task that completes when the connection is established and the receive loop has started.</returns>
    /// <remarks>
    /// This method is not idempotent: a second call after a successful start throws
    /// <see cref="InvalidOperationException"/>. If connection establishment or receive-loop startup fails,
    /// the transport aborts the socket and the instance cannot be reused. A new <see cref="WsTransport"/>
    /// instance (and therefore a new <see cref="ClientWebSocket"/>) is required to reconnect.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the transport has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the transport has already been started.</exception>
    public async Task StartAsync(CancellationToken token = default)
    {
        this.ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref this.startState, 1, 0) != 0)
        {
            throw new InvalidOperationException("The WebSocket transport has already been started.");
        }

        try
        {
            await this.webSocket
                .ConnectAsync(this.uri, token)
                .ConfigureAwait(false);

            this.receiver.Start(token);
        }
        catch
        {
            try
            {
                this.webSocket.Abort();
            }
            catch
            {
                // Best effort: Abort may throw if the socket is already in a terminal state.
            }

            Interlocked.Exchange(ref this.startState, 0);
            throw;
        }
    }

    /// <summary>
    /// Initiates a graceful shutdown of the WebSocket connection when the socket is open.
    /// </summary>
    /// <param name="token">A cancellation token for the close operation.</param>
    /// <returns>A task that completes when the close frame has been sent or when no close was required.</returns>
    /// <remarks>
    /// If the socket is not currently open, this method returns without sending a close frame.
    /// WebSocket close errors are logged and suppressed.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the transport has been disposed.</exception>
    public async Task StopAsync(CancellationToken token = default)
    {
        this.ThrowIfDisposed();

        if (this.webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            // Use CloseOutputAsync (send-only) rather than CloseAsync (send + wait for server
            // close frame). CloseAsync also calls ReceiveAsync internally, which conflicts with
            // the concurrent ReceiveAsync in WsReceiver and can hang on .NET Framework. The
            // receive loop will consume the server's close response and exit naturally.
            await this.webSocket
                .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client closed connection", token)
                .ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            OpAmpClientEventSource.Log.TransportCloseException(ex);
        }
    }

    /// <summary>
    /// Sends an OpAMP message over the active WebSocket connection.
    /// </summary>
    /// <typeparam name="T">The protobuf message type to send.</typeparam>
    /// <param name="message">The message to serialize and transmit.</param>
    /// <param name="token">A cancellation token for the send operation.</param>
    /// <returns>A task that completes when the message has been serialized and written to the socket.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the transport has been disposed.</exception>
    public Task SendAsync<T>(T message, CancellationToken token = default)
        where T : IMessage<T>
    {
        this.ThrowIfDisposed();

        return this.transmitter.SendAsync(message, token);
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        this.webSocket.Abort();
        this.receiver.Dispose();
        this.webSocket.Dispose();
    }

    private void ThrowIfDisposed()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(this.disposed, this);
#else
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(WsTransport));
        }
#endif
    }
}
