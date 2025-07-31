// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using Google.Protobuf;
using OpAmp.Protocol;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests.Tools;

internal class OpAmpFakeHttpServer : IDisposable
{
    private readonly IDisposable httpServer;
    private readonly BlockingCollection<AgentToServer> frames;

    public OpAmpFakeHttpServer()
    {
        this.frames = [];
        this.httpServer = TestHttpServer.RunServer(
            context =>
            {
                var buffer = new byte[context.Request.ContentLength64];
                _ = context.Request.InputStream.Read(buffer, 0, buffer.Length);
                var frame = AgentToServer.Parser.ParseFrom(buffer);

                this.frames.Add(frame);

                var response = new ServerToAgent
                {
                    InstanceUid = frame.InstanceUid,
                    CustomMessage = new CustomMessage
                    {
                        Data = ByteString.CopyFromUtf8("Response from OpAmpFakeHttpServer"),
                    },
                };
                var responseBytes = response.ToByteArray();

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
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
}
