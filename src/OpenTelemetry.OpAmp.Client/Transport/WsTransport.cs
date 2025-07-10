// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;
using Google.Protobuf;

namespace OpenTelemetry.OpAmp.Client.Transport;

internal class WsTransport : IOpAmpTransport, IDisposable
{
    private readonly Uri uri = new("wss://localhost:4320/v1/opamp");
    private readonly SocketsHttpHandler handler = new();
    private readonly ClientWebSocket ws = new();
    private readonly WsReceiver receiver;
    private readonly WsTransmitter transmitter;
    private readonly FrameProcessor processor;

    public WsTransport(FrameProcessor processor)
    {
        // trust all certificates
        this.handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
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
