// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Net.WebSockets;
using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Utils;

namespace OpenTelemetry.OpAmp.Client.Transport.WebSocket;

internal sealed class WsTransmitter
{
    private const int BufferSize = 4096;

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly ClientWebSocket ws;

    public WsTransmitter(ClientWebSocket ws)
    {
        Guard.ThrowIfNull(ws, nameof(ws));

        this.ws = ws;
    }

    public async Task SendAsync(IMessage message, CancellationToken token = default)
    {
        var headerSize = OpAmpWsHeaderHelper.WriteHeader(this.buffer);
        var size = message.CalculateSize();

        // fits to the buffer, send completely
        if (size + headerSize <= BufferSize)
        {
            var segment = new ArraySegment<byte>(this.buffer, headerSize, size);
            message.WriteTo(segment);

            // resegment to include the header byte
            segment = new ArraySegment<byte>(this.buffer, 0, size + headerSize);

            await this.ws.SendAsync(segment, WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
        }

        // Does not fit, need to chunk the message
        else
        {
            // TODO: Implement chunking logic for large messages
            throw new NotImplementedException();
        }
    }
}

#endif
