// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net.WebSockets;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Utils;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal class OpAmpFakeWebSocketServer : IDisposable
{
    private readonly IDisposable httpServer;
    private readonly BlockingCollection<AgentToServer> frames = [];

    public OpAmpFakeWebSocketServer()
    {
        this.httpServer = TestWebSocketServer.RunServer(
            async socket =>
            {
                var buffer = new byte[8 * 1024];
                var ms = new MemoryStream();

                try
                {
                    while (socket.State == WebSocketState.Open)
                    {
                        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);

                            break;
                        }

                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }

                        if (result.EndOfMessage)
                        {
                            var frame = ProcessReceive(ms);
                            this.frames.Add(frame);

                            var response = GenerateResponse(frame);
                            await socket.SendAsync(response, WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    ms.Dispose();

                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server disposed", CancellationToken.None);
                    }
                }
            },
            out var host,
            out var port);

        this.Endpoint = new Uri($"ws://{host}:{port}/v1/opamp");
    }

    public Uri Endpoint { get; }

    public IReadOnlyCollection<AgentToServer> GetFrames()
    {
        return this.frames.ToArray();
    }

    public void Dispose()
    {
        this.httpServer.Dispose();
    }

    private static AgentToServer ProcessReceive(MemoryStream ms)
    {
        var fullMessageBytes = ms.ToArray();
        OpAmpWsHeaderHelper.TryVerifyHeader(fullMessageBytes, out var headerSize);

        var messageBytes = fullMessageBytes.AsSpan().Slice(headerSize, fullMessageBytes.Length - headerSize);
        ms.SetLength(0);

        // Parse protobuf message from the websocket message bytes.
        var frame = AgentToServer.Parser.ParseFrom(messageBytes);

        return frame;
    }

    private static ArraySegment<byte> GenerateResponse(AgentToServer frame)
    {
        var response = new ServerToAgent
        {
            InstanceUid = frame.InstanceUid,
            CustomMessage = new CustomMessage
            {
                Data = ByteString.CopyFromUtf8("Response from OpAmpFakeWebSocketServer"),
            },
        };

        var responseLength = response.CalculateSize();
        var responseBuffer = new byte[responseLength + OpAmpWsHeaderHelper.MaxHeaderLength];

        var headerSize = OpAmpWsHeaderHelper.WriteHeader(new ArraySegment<byte>(responseBuffer));

        var segment = new ArraySegment<byte>(responseBuffer, headerSize, responseLength);
        response.WriteTo(segment);

        // resegment to include the header byte
        segment = new ArraySegment<byte>(responseBuffer, 0, responseLength + headerSize);

        return segment;
    }
}
