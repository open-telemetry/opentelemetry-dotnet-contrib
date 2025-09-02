// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Net.WebSockets;
using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport;

namespace OpenTelemetry.OpAmp.Client.Transport.WebSocket;

internal class WsTransport : IOpAmpTransport, IDisposable
{
    private readonly Uri uri;
    private readonly SocketsHttpHandler handler = new();
    private readonly ClientWebSocket ws = new();
    private readonly WsReceiver receiver;
    private readonly WsTransmitter transmitter;
    private readonly FrameProcessor processor;

    public WsTransport(Uri serverUrl, FrameProcessor processor)
    {
        Guard.ThrowIfNull(serverUrl, nameof(serverUrl));
        Guard.ThrowIfNull(processor, nameof(processor));

        // TODO: fix trust all certificates
#pragma warning disable CA5359 // Do Not Disable Certificate Validation
        this.handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
#pragma warning restore CA5359 // Do Not Disable Certificate Validation
        this.uri = serverUrl;
        this.processor = processor;
        this.receiver = new WsReceiver(this.ws, this.processor);
        this.transmitter = new WsTransmitter(this.ws);
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        using var invoker = new HttpMessageInvoker(this.handler);

        await this.ws
            .ConnectAsync(this.uri, invoker, token)
            .ConfigureAwait(false);

        this.receiver.Start(token);
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        await this.ws
            .CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed connection", token)
            .ConfigureAwait(false);
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

#endif
