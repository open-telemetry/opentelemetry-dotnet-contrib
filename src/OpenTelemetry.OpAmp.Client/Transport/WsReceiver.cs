// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Net.WebSockets;
using OpenTelemetry.OpAmp.Client.Utils;

namespace OpenTelemetry.OpAmp.Client.Transport;

internal class WsReceiver : IDisposable
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
        this.ws = ws ?? throw new ArgumentNullException(nameof(ws));
        this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
        this.receiveThread = new Thread(this.ReceiveLoop);
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
            if (this.token.IsCancellationRequested)
            {
                this.token.ThrowIfCancellationRequested();
            }

            await this.ReceiveAsync(this.token).ConfigureAwait(false);
        }
    }

    private async Task ReceiveAsync(CancellationToken token)
    {
        var totalCount = 0;
        var workingCount = 0;
        WebSocketReceiveResult result;
        byte[] workingBuffer = this.receiveBuffer;

        List<byte[]>? rentalBuffers = null;

        do
        {
            if (this.token.IsCancellationRequested)
            {
                this.token.ThrowIfCancellationRequested();
            }

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

            var segment1 = new ArraySegment<byte>(workingBuffer, workingCount, workingBuffer.Length - workingCount);
            result = await this.ws.ReceiveAsync(segment1, token).ConfigureAwait(false);

            workingCount += result.Count;
            totalCount += result.Count;
        }
        while (!result.EndOfMessage);

        var sequence =
            rentalBuffers?.Count > 1
                ? SequenceHelper.CreateSequenceFromBuffers(rentalBuffers, workingCount + 1)
                : new ReadOnlySequence<byte>(this.receiveBuffer);

        this.processor.OnServerFrame(sequence, totalCount, verifyHeader: true);

        ReturnRentalBuffers(rentalBuffers);
    }
}
