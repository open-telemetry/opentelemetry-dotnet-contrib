// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using OpAmp.Proto.V1;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal class OpAmpFakeHttpServer : IDisposable
{
    private readonly IDisposable httpServer;
    private readonly BlockingCollection<AgentToServer> frames = [];

    public OpAmpFakeHttpServer(bool useSmallPackets)
    {
        this.httpServer = TestHttpServer.RunServer(
            context =>
            {
                var frame = ProcessReceive(context.Request);
                this.frames.Add(frame);

                var response = GenerateResponse(frame, useSmallPackets);

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.OutputStream.Write(response.Array!, response.Offset, response.Count);
                context.Response.Close();
            },
            out var host,
            out var port);
        this.Endpoint = new Uri($"http://{host}:{port}");
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

    private static AgentToServer ProcessReceive(HttpListenerRequest request)
    {
        var buffer = new byte[request.ContentLength64];
        _ = request.InputStream.Read(buffer, 0, buffer.Length);

        var frame = AgentToServer.Parser.ParseFrom(buffer);

        return frame;
    }

    private static ArraySegment<byte> GenerateResponse(AgentToServer frame, bool useSmallPackets)
    {
        var response = FrameGenerator.GenerateMockServerFrame(frame.InstanceUid, useSmallPackets, addHeader: false);

        return response.Frame;
    }
}
