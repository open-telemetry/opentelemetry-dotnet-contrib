// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.WebSockets;
using Google.Protobuf;
using OpenTelemetry.Internal;

namespace OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;

internal sealed class WsTransport : IOpAmpTransport, IDisposable
{
    private readonly Uri uri;
    private readonly HttpClientHandler handler = new();
    private readonly ClientWebSocket ws = new();
    private readonly WsReceiver receiver;
    private readonly WsTransmitter transmitter;
    private readonly FrameProcessor processor;

    public WsTransport(Uri serverUrl, FrameProcessor processor)
    {
        Guard.ThrowIfNull(serverUrl, nameof(serverUrl));
        Guard.ThrowIfNull(processor, nameof(processor));

        // TODO: fix trust all certificates
#if NET
        this.handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        this.uri = serverUrl;
        this.processor = processor;
        this.receiver = new WsReceiver(this.ws, this.processor);
        this.transmitter = new WsTransmitter(this.ws);
    }

    public async Task StartAsync(CancellationToken token = default)
    {
#if NET
        using var invoker = new HttpMessageInvoker(this.handler);
#endif

        await this.ws
#if NET
            .ConnectAsync(this.uri, invoker, token)
#else
            .ConnectAsync(this.uri, token)
#endif
            .ConfigureAwait(false);

        this.receiver.Start(token);
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        if (this.ws.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await this.ws
                .CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed connection", token)
                .ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            // The WsReceiver may consume the peer's close frame before
            // CloseAsync sees it, causing a WebSocketException under load.
            OpAmpClientEventSource.Log.TransportCloseException(ex);
        }
    }

    public Task SendAsync<T>(T message, CancellationToken token = default)
        where T : IMessage<T>
    {
        return this.transmitter.SendAsync(message, token);
    }

    public void Dispose()
    {
        this.handler.Dispose();
        this.ws.Dispose();
        this.receiver.Dispose();
    }
}
