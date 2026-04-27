// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
#endif

using System.IO.Compression;
using System.Net;
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
        // Arrange - stand up a fake server that returns a response body larger than the 128 KB transport limit.
        // SendChunked suppresses the Content-Length header so this test exercises the body-read limit,
        // not the Content-Length pre-check (which has its own test below).
        var oversizedBody = new byte[TransportConstants.MaxMessageSize + 1];
        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
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
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None));
    }

    [Fact]
#if NETFRAMEWORK
    [SuppressMessage("Security", "CA5399:Enable HttpClient certificate revocation list check", Justification = "Causes PlatformNotSupportedException at runtime on net462")]
#endif
    public async Task PlainHttpTransport_RejectsOversizedCompressedResponse()
    {
        // Arrange - server sends a gzip-compressed response where the compressed payload is within
        // MaxMessageSize (so the Content-Length pre-check is bypassed) but the decompressed body
        // exceeds it. When HttpClient transparently decompresses the stream, the body-read loop
        // must still enforce the limit on the decompressed bytes.
        var largeBody = new byte[TransportConstants.MaxMessageSize + 1];

        byte[] compressedBody;
        using (var ms = new MemoryStream())
        {
            using (var gzip = new GZipStream(ms, CompressionLevel.Optimal))
            {
                gzip.Write(largeBody, 0, largeBody.Length);
            }

            compressedBody = ms.ToArray();
        }

        // All-zeroes compress extremely well; assert the compressed size is within the limit so
        // the test genuinely exercises the body-read path rather than the Content-Length check.
        Assert.True(
            compressedBody.Length < TransportConstants.MaxMessageSize,
            $"Compressed body ({compressedBody.Length} bytes) must be smaller than MaxMessageSize ({TransportConstants.MaxMessageSize}) for this test to be meaningful.");

        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.Headers["Content-Encoding"] = "gzip";
                context.Response.ContentLength64 = compressedBody.Length;
                context.Response.OutputStream.Write(compressedBody, 0, compressedBody.Length);
                context.Response.Close();
            },
            out var host,
            out var port);

        var settings = new OpAmpClientSettings
        {
            ServerUrl = new Uri($"http://{host}:{port}"),
            HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip,
#if NET
                    CheckCertificateRevocationList = true,
#endif
                };
                return new HttpClient(handler);
            },
        };

        var frameProcessor = new FrameProcessor();
        using var httpTransport = new PlainHttpTransport(settings, frameProcessor);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(true);

        // Act & Assert - the decompressed body exceeds MaxMessageSize so the body-read
        // limit must fire even though the Content-Length (showing compressed size) does not.
        await Assert.ThrowsAsync<InvalidOperationException>(
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
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
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
                catch (HttpListenerException)
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
            var timeout = TimeSpan.FromSeconds(2);

#if NET
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await sendTask.WaitAsync(timeout));
#else
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(sendTask, Task.Delay(timeout, cts.Token));
            Assert.Same(sendTask, completedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await sendTask);
#endif
        }
        finally
        {
            allowServerToFinish.Set();
        }
    }

    [Fact]
    public async Task PlainHttpTransport_RejectsResponseWithOversizedContentLength()
    {
        // Arrange - server advertises a Content-Length larger than the limit.
        // The Content-Length pre-check in the transport should reject this before reading the body,
        // so the client may disconnect immediately after the headers are flushed.
        var oversizedLength = TransportConstants.MaxMessageSize + 1;
        using var opAmpServer = TestHttpServer.RunServer(
            context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/x-protobuf";
                context.Response.ContentLength64 = oversizedLength;

                try
                {
                    context.Response.OutputStream.WriteByte(0);
                    context.Response.OutputStream.Flush();
                }
                catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
                {
                    // The client may close the connection as soon as it sees the oversized Content-Length.
                }
                finally
                {
                    try
                    {
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => httpTransport.SendAsync(mockFrame.Frame, cts.Token));
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
                context.Response.StatusCode = (int)HttpStatusCode.OK;
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
        // throw, but the key assertion is that InvalidOperationException is not thrown.
        var ex = await Record.ExceptionAsync(
            () => httpTransport.SendAsync(mockFrame.Frame, CancellationToken.None));

        // Assert
        Assert.False(
            ex is InvalidOperationException,
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
