// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Net.WebSockets;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Utils;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal class OpAmpFakeWebSocketServer : IDisposable
{
    private readonly IDisposable httpServer;
    private readonly BlockingCollection<AgentToServer> frames = [];
    private readonly BlockingCollection<NameValueCollection> requestHeaders = [];
    private readonly BlockingCollection<WebSocketCloseStatus?> clientCloseStatuses = [];
    private readonly CancellationTokenSource cts = new();

    public OpAmpFakeWebSocketServer(bool useSmallPackets)
        : this((frame, socket, token) =>
        {
            var response = GenerateResponse(frame, useSmallPackets);
            return socket.SendAsync(response, WebSocketMessageType.Binary, true, token);
        })
    {
    }

    public OpAmpFakeWebSocketServer(Func<AgentToServer, WebSocket, CancellationToken, Task> responseHandler)
    {
        this.httpServer = TestWebSocketServer.RunServer(
            async (context, socket) =>
            {
                this.requestHeaders.Add(context.Request.Headers);

                var buffer = new byte[8 * 1024];
                using var ms = new MemoryStream();

                try
                {
                    while (socket.State == WebSocketState.Open)
                    {
                        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), this.cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            this.clientCloseStatuses.Add(result.CloseStatus);
                            await socket.CloseAsync(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, this.cts.Token);

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

                            await responseHandler(frame, socket, this.cts.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (WebSocketException)
                {
                    // The peer may abort the socket during disposal.
                }
                catch (ObjectDisposedException)
                {
                    // The peer may abort the socket during disposal.
                }
                finally
                {
                    if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server disposed", CancellationToken.None);
                        }
                        catch (WebSocketException)
                        {
                            // The peer may have already closed or aborted the connection.
                        }
                    }
                }
            },
            out var host,
            out var port);

        this.Endpoint = new Uri($"ws://{host}:{port}/v1/opamp");
    }

    public Uri Endpoint { get; }

    public IReadOnlyCollection<AgentToServer> GetFrames()
        => [.. this.frames];

    public IReadOnlyCollection<NameValueCollection> GetRequestHeaders()
        => [.. this.requestHeaders];

    public bool TryGetClientCloseStatus(TimeSpan timeout, out WebSocketCloseStatus? closeStatus)
        => this.clientCloseStatuses.TryTake(out closeStatus, (int)timeout.TotalMilliseconds);

    public void Dispose()
    {
        this.cts.Cancel();
        this.cts.Dispose();
        this.httpServer.Dispose();
    }

    private static AgentToServer ProcessReceive(MemoryStream ms)
    {
        var fullMessageBytes = new ReadOnlySequence<byte>(ms.ToArray());
        var result = OpAmpWsHeaderHelper.TryVerifyHeader(fullMessageBytes, out var headerSize, out var errorMessage);
        if (!result)
        {
            throw new InvalidOperationException(errorMessage);
        }

        var messageBytes = fullMessageBytes.Slice(headerSize, fullMessageBytes.Length - headerSize);
        ms.SetLength(0);

        // Parse protobuf message from the websocket message bytes.
        var frame = AgentToServer.Parser.ParseFrom(messageBytes);

        return frame;
    }

    private static ArraySegment<byte> GenerateResponse(AgentToServer frame, bool useSmallPackets)
    {
        var response = FrameGenerator.GenerateMockServerFrame(frame.InstanceUid, useSmallPackets, addHeader: true);

        return response.Frame;
    }
}
