// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using System.Net.WebSockets;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.Transport.WebSocket;

internal sealed class WsReceiver : IDisposable
{
    private const int RentalBufferSize = 4096;
    private const int ReceiveBufferSize = 8192;

    private readonly ClientWebSocket ws;
    private readonly Thread receiveThread;
    private readonly FrameProcessor processor;

    private readonly byte[] receiveBuffer = new byte[ReceiveBufferSize];

    private CancellationToken token;

    public WsReceiver(ClientWebSocket ws, FrameProcessor processor)
    {
        Guard.ThrowIfNull(ws, nameof(ws));
        Guard.ThrowIfNull(processor, nameof(processor));

        this.ws = ws;
        this.processor = processor;
        this.receiveThread = new Thread(this.ReceiveLoop)
        {
            Name = "OpAmp WebSocket Receive Loop",
        };
    }

    public void Start(CancellationToken token = default)
    {
        this.token = token;
        this.receiveThread.Start();
    }

    public void Dispose()
    {
        this.receiveThread?.Join();
    }

    private static void ReturnRentalBuffers(List<byte[]>? rentalBuffers)
    {
        if (rentalBuffers == null)
        {
            return;
        }

        foreach (var rental in rentalBuffers)
        {
            ArrayPool<byte>.Shared.Return(rental);
        }
    }

    private async void ReceiveLoop()
    {
        while (true)
        {
            this.token.ThrowIfCancellationRequested();

            if (this.ws.State != WebSocketState.Open)
            {
                // Connection is closed, dont start a new loop
                break;
            }

            await this.ReceiveAsync(this.token).ConfigureAwait(false);
        }
    }

    private async Task ReceiveAsync(CancellationToken token)
    {
        var totalCount = 0;
        var workingCount = 0;
        var isClosed = false;
        WebSocketReceiveResult result;
        byte[] workingBuffer = this.receiveBuffer;

        List<byte[]>? rentalBuffers = null;

        do
        {
            this.token.ThrowIfCancellationRequested();

            // out of space, need to rent more
            if (workingBuffer.Length - workingCount == 0)
            {
                if (rentalBuffers == null)
                {
                    rentalBuffers = [this.receiveBuffer];
                }

                workingBuffer = ArrayPool<byte>.Shared.Rent(RentalBufferSize);
                workingCount = 0;
                rentalBuffers.Add(workingBuffer);
            }

            if (this.ws.State != WebSocketState.Open)
            {
                // Connection is closed, nothing more to read.
                isClosed = true;
                break;
            }

            var segment1 = new ArraySegment<byte>(workingBuffer, workingCount, workingBuffer.Length - workingCount);
            result = await this.ws.ReceiveAsync(segment1, token).ConfigureAwait(false);

            isClosed = result.CloseStatus != null;
            workingCount += result.Count;
            totalCount += result.Count;
        }
        while (!result.EndOfMessage);

        if (!isClosed)
        {
            var sequence =
                rentalBuffers?.Count > 1
                    ? rentalBuffers.CreateSequenceFromBuffers(workingCount + 1)
                    : new ReadOnlySequence<byte>(this.receiveBuffer);

            this.processor.OnServerFrame(sequence, totalCount, verifyHeader: true);
        }

        ReturnRentalBuffers(rentalBuffers);
    }
}

#endif
