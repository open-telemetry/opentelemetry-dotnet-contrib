// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

using System.Text;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class PlainHttpTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PlainHttpTransport_SendReceiveCommunication(bool useSmallPackets)
    {
        // Arrange
        using var opAmpServer = new OpAmpFakeHttpServer(useSmallPackets);
        var opAmpEndpoint = opAmpServer.Endpoint;
        var settings = new OpAmpClientSettings { ServerUrl = opAmpEndpoint };

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);

        var mockFrame = FrameGenerator.GenerateMockAgentFrame(useSmallPackets);

        // Act
        await httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None);

        // Assert
        var serverReceivedFrames = opAmpServer.GetFrames();
        var clientReceivedFrames = mockListener.Messages;
        var receivedTextData =
#if NET
            Encoding.UTF8.GetString(clientReceivedFrames.First().Data);
#else
            Encoding.UTF8.GetString([.. clientReceivedFrames.First().Data]);
#endif

        var frame = Assert.Single(serverReceivedFrames);
        Assert.Equal(mockFrame.Uid, frame.InstanceUid);

        Assert.Single(clientReceivedFrames);
        Assert.StartsWith("This is a mock server frame for testing purposes.", receivedTextData);
    }

    [Fact]
    public async Task PlainHttpTransport_UsesConfiguredHttpClientFactory()
    {
        // Arrange
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;
        var settings = new OpAmpClientSettings
        {
            ServerUrl = opAmpEndpoint,
            HttpClientFactory = () =>
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-Custom-Header", "CustomValue");
                return client;
            },
        };

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(false);

        // Act
        await httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None);

        // Assert
        var serverReceivedHeaders = opAmpServer.GetHeaders();
        Assert.Contains(serverReceivedHeaders, headers => headers["X-Custom-Header"] == "CustomValue");
    }

    [Fact]
    public async Task PlainHttpTransport_RejectsOversizedResponse()
    {
        // Arrange — stand up a fake server that returns a response body larger than the 128 KB transport limit.
        // SendChunked suppresses the Content-Length header so this test exercises the body-read limit,
        // not the Content-Length pre-check (which has its own test below).
        var oversizedBody = new byte[TransportConstants.MaxMessageSize + 1];
        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                context.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.SendChunked = true;
                context.Response.OutputStream.Write(oversizedBody, 0, oversizedBody.Length);
                context.Response.Close();
            },
            out var host,
            out var port);

        var settings = new OpAmpClientSettings
        {
            ServerUrl = new Uri($"http://{host}:{port}"),
        };

        var frameProcessor = new FrameProcessor();
        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(true);

        // Act & Assert
        await Assert.ThrowsAsync<OpAmpOversizedResponseException>(
            () => httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None));
    }

    [Fact]
    public async Task PlainHttpTransport_RejectsOversizedChunkedResponseBeforeServerCompletesBody()
    {
        using var thresholdReached = new ManualResetEventSlim();
        using var allowServerToFinish = new ManualResetEventSlim();

        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                try
                {
                    context.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                    context.Response.ContentType = "application/x-protobuf";
                    context.Response.SendChunked = true;

                    var chunk = new byte[4096];
                    var remaining = TransportConstants.MaxMessageSize + 1;
                    while (remaining > 0)
                    {
                        var bytesToWrite = Math.Min(chunk.Length, remaining);
                        context.Response.OutputStream.Write(chunk, 0, bytesToWrite);
                        context.Response.OutputStream.Flush();
                        remaining -= bytesToWrite;
                    }

                    thresholdReached.Set();

                    allowServerToFinish.Wait(TimeSpan.FromSeconds(10));

                    context.Response.OutputStream.WriteByte(0);
                    context.Response.Close();
                }
                catch (System.Net.HttpListenerException)
                {
                    thresholdReached.Set();
                }
                catch (ObjectDisposedException)
                {
                    thresholdReached.Set();
                }
            },
            out var host,
            out var port);

        var settings = new OpAmpClientSettings
        {
            ServerUrl = new Uri($"http://{host}:{port}"),
        };

        var frameProcessor = new FrameProcessor();
        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(true);

        var sendTask = httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None);

        Assert.True(thresholdReached.Wait(TimeSpan.FromSeconds(5)), "The server did not send enough bytes to exceed the transport limit.");

        try
        {
            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.Same(sendTask, completedTask);
            await Assert.ThrowsAsync<OpAmpOversizedResponseException>(async () => await sendTask);
        }
        finally
        {
            allowServerToFinish.Set();
        }
    }

    [Fact]
    public async Task PlainHttpTransport_RejectsResponseWithOversizedContentLength()
    {
        // Arrange — server advertises and sends a Content-Length larger than the limit.
        // The Content-Length pre-check in the transport should reject this before reading the body.
        var oversizedBody = new byte[TransportConstants.MaxMessageSize + 1];
        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                context.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.ContentLength64 = oversizedBody.Length;
                context.Response.OutputStream.Write(oversizedBody, 0, oversizedBody.Length);
                context.Response.Close();
            },
            out var host,
            out var port);

        var settings = new OpAmpClientSettings
        {
            ServerUrl = new Uri($"http://{host}:{port}"),
        };

        var frameProcessor = new FrameProcessor();
        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(true);

        // Act & Assert
        await Assert.ThrowsAsync<OpAmpOversizedResponseException>(
            () => httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None));
    }

    [Fact]
    public async Task PlainHttpTransport_AcceptsResponseAtExactMaxSize()
    {
        // Arrange - response body is exactly MaxMessageSize bytes (the boundary).
        // The bounded read should accept this; only responses strictly exceeding the limit are rejected.
        var body = new byte[TransportConstants.MaxMessageSize];
        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                context.Response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.SendChunked = true;
                context.Response.OutputStream.Write(body, 0, body.Length);
                context.Response.Close();
            },
            out var host,
            out var port);

        var settings = new OpAmpClientSettings
        {
            ServerUrl = new Uri($"http://{host}:{port}"),
        };

        var frameProcessor = new FrameProcessor();
        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(true);

        // Act - the response is exactly at the limit so it should NOT be rejected as oversized.
        // The body (zeroed bytes) is not a valid ServerToAgent message, so protobuf parsing may
        // throw, but the key assertion is that OpAmpOversizedResponseException is not thrown.
        var ex = await Record.ExceptionAsync(
            () => httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None));

        // Assert
        Assert.False(
            ex is OpAmpOversizedResponseException,
            "A response at exactly MaxMessageSize should not be rejected as oversized.");
    }

    [Fact]
    public async Task PlainHttpTransport_SendsInstanceUUIDHeader()
    {
        // Arrange
        using var opAmpServer = new OpAmpFakeHttpServer(false);

        var uuid = Guid.NewGuid();
        var settings = new OpAmpClientSettings
        {
            ServerUrl = opAmpServer.Endpoint,
            InstanceUid = uuid,
        };

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(false);

        // Act
        await httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None);

        // Assert
        var serverReceivedHeaders = opAmpServer.GetHeaders();
        Assert.Contains(serverReceivedHeaders, headers => headers["OpAMP-Instance-UID"] == uuid.ToString());
    }
}
