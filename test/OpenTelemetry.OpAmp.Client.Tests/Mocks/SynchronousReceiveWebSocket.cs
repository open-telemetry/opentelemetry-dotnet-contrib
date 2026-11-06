// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net.WebSockets;

namespace OpenTelemetry.OpAmp.Client.Tests.Mocks;

internal sealed class SynchronousReceiveWebSocket : WebSocket
{
    private readonly ArraySegment<byte> frame;
    private readonly int frameCount;
    private WebSocketState state = WebSocketState.Open;

    public SynchronousReceiveWebSocket(ArraySegment<byte> frame, int frameCount)
    {
        this.frame = frame;
        this.frameCount = frameCount;
    }

    public override WebSocketCloseStatus? CloseStatus => null;

    public override string? CloseStatusDescription => null;

    public override WebSocketState State => this.state;

    public override string? SubProtocol => null;

    public ManualResetEventSlim FramesReceived { get; } = new();

    public int ReceiveCount { get; private set; }

    public Thread? ReceiveThread { get; private set; }

    public override void Abort() => this.state = WebSocketState.Aborted;

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public override void Dispose()
    {
        this.state = WebSocketState.Closed;
        this.FramesReceived.Dispose();
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.ReceiveThread = Thread.CurrentThread;
        Array.Copy(this.frame.Array!, this.frame.Offset, buffer.Array!, buffer.Offset, this.frame.Count);

        // Every frame is already buffered: the receiver never gets an asynchronous suspension.
        // Stop after a finite stream so inline startup can fail an assertion without hanging.
        if (++this.ReceiveCount == this.frameCount)
        {
            this.state = WebSocketState.Closed;
            this.FramesReceived.Set();
        }

        return Task.FromResult(new WebSocketReceiveResult(this.frame.Count, WebSocketMessageType.Binary, endOfMessage: true));
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
