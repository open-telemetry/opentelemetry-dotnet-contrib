// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Net.WebSockets;
using System.Text;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class WsTransportTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WsTransport_SendReceiveCommunication(bool useSmallPackets)
    {
        // Arrange
        using var opAmpServer = new OpAmpFakeWebSocketServer(useSmallPackets);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var cts = new CancellationTokenSource();
        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = opAmpEndpoint,
        };
        using var wsTransport = new WsTransport(settings, frameProcessor);
        await wsTransport.StartAsync(cts.Token);

        var mockFrame = FrameGenerator.GenerateMockAgentFrame(useSmallPackets);

        // Act
        await wsTransport.SendAsync(mockFrame.Frame, cts.Token);

        mockListener.WaitForMessages(TimeSpan.FromSeconds(30));

        await wsTransport.StopAsync(cts.Token);

        cts.Cancel();

        // Assert
        var serverReceivedFrames = opAmpServer.GetFrames();
        var clientReceivedFrames = mockListener.Messages;
        var receivedTextData =
#if NET
            Encoding.UTF8.GetString(clientReceivedFrames.First().Data);
#else
            Encoding.UTF8.GetString([.. clientReceivedFrames.First().Data]);
#endif

        Assert.Single(serverReceivedFrames);
        Assert.Equal(mockFrame.Uid, serverReceivedFrames.First().InstanceUid);

        Assert.Single(clientReceivedFrames);
        Assert.StartsWith("This is a mock server frame for testing purposes.", receivedTextData);
    }

    [Fact]
    public async Task WsTransport_UsesConfiguredClientWebSocketFactory()
    {
        using var opAmpServer = new OpAmpFakeWebSocketServer(useSmallPackets: false);

        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = opAmpServer.Endpoint,
            ClientWebSocketFactory = () =>
            {
                var clientWebSocket = new ClientWebSocket();
                clientWebSocket.Options.SetRequestHeader("X-Test-Header", "ConfiguredByFactory");
                return clientWebSocket;
            },
        };

        var frameProcessor = new FrameProcessor();

        using var wsTransport = new WsTransport(settings, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.StopAsync(CancellationToken.None);

        var requestHeaders = Assert.Single(opAmpServer.GetRequestHeaders());
        Assert.Equal("ConfiguredByFactory", requestHeaders["X-Test-Header"]);
    }

    [Fact]
    public async Task WsTransport_RejectsOversizedFragmentedResponseBeforeEndOfMessage()
    {
        using var thresholdReached = new ManualResetEventSlim();
        var oversizedFrame = FrameGenerator.GenerateMockServerFrameOfTotalSize(TransportConstants.MaxMessageSize + 1, addHeader: true);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            async (frame, socket, token) =>
            {
                var boundarySegment = new ArraySegment<byte>(oversizedFrame.Frame.Array!, oversizedFrame.Frame.Offset, TransportConstants.MaxMessageSize);
                var overflowSegment = new ArraySegment<byte>(oversizedFrame.Frame.Array!, oversizedFrame.Frame.Offset + TransportConstants.MaxMessageSize, 1);

                await socket.SendAsync(boundarySegment, WebSocketMessageType.Binary, false, token).ConfigureAwait(false);
                await socket.SendAsync(overflowSegment, WebSocketMessageType.Binary, false, token).ConfigureAwait(false);
                thresholdReached.Set();
            });

        var frameProcessor = new FrameProcessor();
        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);

        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        var timeout = TimeSpan.FromSeconds(5);

        Assert.True(thresholdReached.Wait(timeout), "The server did not send enough bytes to exceed the transport limit.");
        Assert.True(opAmpServer.TryGetClientCloseStatus(timeout, out var closeStatus));
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, closeStatus);
    }

    [Fact]
    public async Task WsTransport_AcceptsLargeFragmentedMessageAcrossRentalBuffers()
    {
        var largeFrame = FrameGenerator.GenerateMockServerFrame(useSmallPackets: false, addHeader: true);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => SendMessageInChunksAsync(socket, largeFrame.Frame, WebSocketMessageType.Binary, chunkSize: 1024, token));

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.True(mockListener.TryWaitForMessage(TimeSpan.FromSeconds(5)), "The client did not receive the large fragmented response.");

#if NET
        var receivedTextData = Encoding.UTF8.GetString(mockListener.Messages.Single().Data);
#else
        var receivedTextData = Encoding.UTF8.GetString([.. mockListener.Messages.Single().Data]);
#endif

        Assert.Equal(largeFrame.ExpectedContent, receivedTextData);

        await wsTransport.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WsTransport_DropsInvalidBinaryFrameWithoutDispatchingMessage()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        var invalidFrame = new ArraySegment<byte>([0x01]);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => socket.SendAsync(invalidFrame, WebSocketMessageType.Binary, true, token));

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.False(mockListener.TryWaitForMessage(TimeSpan.FromMilliseconds(500)));
        Assert.Empty(mockListener.Messages);
        await WaitForEventAsync(eventListener, nameof(OpAmpClientEventSource.InvalidWsFrame), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WsTransport_ContinuesAfterInvalidBinaryFrame()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        var invalidFrame = new ArraySegment<byte>([0x01]);
        var validFrame = FrameGenerator.GenerateMockServerFrame(addHeader: true);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            async (frame, socket, token) =>
            {
                await socket.SendAsync(invalidFrame, WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
                await socket.SendAsync(validFrame.Frame, WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
            });

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        var timeout = TimeSpan.FromSeconds(5);

        Assert.True(mockListener.TryWaitForMessage(timeout), "The client did not receive the valid response after the invalid frame.");
        await WaitForEventAsync(eventListener, nameof(OpAmpClientEventSource.InvalidWsFrame), timeout);

#if NET
        var receivedTextData = Encoding.UTF8.GetString(mockListener.Messages.Single().Data);
#else
        var receivedTextData = Encoding.UTF8.GetString([.. mockListener.Messages.Single().Data]);
#endif

        Assert.Equal(validFrame.ExpectedContent, receivedTextData);

        await wsTransport.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WsTransport_AcceptsResponseAtExactMaxSize()
    {
        var exactFrame = FrameGenerator.GenerateMockServerFrameOfTotalSize(TransportConstants.MaxMessageSize, addHeader: true);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => socket.SendAsync(exactFrame.Frame, WebSocketMessageType.Binary, true, token));

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.True(mockListener.TryWaitForMessage(TimeSpan.FromSeconds(5)), "The client did not receive the exact-boundary response.");
        Assert.Single(mockListener.Messages);

        await wsTransport.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WsTransport_DropsOversizedResponseWithoutDispatchingFrame()
    {
        var oversizedFrame = FrameGenerator.GenerateMockServerFrameOfTotalSize(TransportConstants.MaxMessageSize + 1, addHeader: true);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => socket.SendAsync(oversizedFrame.Frame, WebSocketMessageType.Binary, true, token));

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.True(opAmpServer.TryGetClientCloseStatus(TimeSpan.FromSeconds(5), out var closeStatus));
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, closeStatus);
        Assert.False(mockListener.TryWaitForMessage(TimeSpan.FromMilliseconds(500)));
        Assert.Empty(mockListener.Messages);
    }

    [Fact]
    public async Task WsTransport_DropsPartialMessageWhenServerClosesBeforeEndOfMessage()
    {
        var partialFrame = FrameGenerator.GenerateMockServerFrame(addHeader: true);
        var partialLength = partialFrame.Frame.Count / 2;

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            async (frame, socket, token) =>
            {
                var partialSegment = Slice(partialFrame.Frame, 0, partialLength);
                await socket.SendAsync(partialSegment, WebSocketMessageType.Binary, false, token).ConfigureAwait(false);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed before end of message", token).ConfigureAwait(false);
            });

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.False(mockListener.TryWaitForMessage(TimeSpan.FromMilliseconds(500)));
        Assert.Empty(mockListener.Messages);
    }

    [Fact]
    public async Task WsTransport_DropsTextFrameWithoutDispatchingMessage()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        var textFrame = new ArraySegment<byte>(Encoding.UTF8.GetBytes("not-opamp"));

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => socket.SendAsync(textFrame, WebSocketMessageType.Text, true, token));

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.False(mockListener.TryWaitForMessage(TimeSpan.FromMilliseconds(500)));
        Assert.Empty(mockListener.Messages);
        await WaitForEventAsync(eventListener, nameof(OpAmpClientEventSource.InvalidWsFrame), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WsTransport_LogsOversizedResponseWarning()
    {
        var timeout = TimeSpan.FromSeconds(5);

        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);

        var oversizedFrame = FrameGenerator.GenerateMockServerFrameOfTotalSize(TransportConstants.MaxMessageSize + 1, addHeader: true);

        using var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => socket.SendAsync(oversizedFrame.Frame, WebSocketMessageType.Binary, true, token));

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, new FrameProcessor());
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);

        Assert.True(opAmpServer.TryGetClientCloseStatus(timeout, out var closeStatus));
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, closeStatus);

        var oversizedEvent = await WaitForEventAsync(eventListener, nameof(OpAmpClientEventSource.OversizedWebSocketMessage), timeout);
        Assert.Equal(EventLevel.Warning, oversizedEvent.Level);
        Assert.Equal(TransportConstants.MaxMessageSize + 1, Assert.IsType<int>(oversizedEvent.Payload![0]));
        Assert.Equal(TransportConstants.MaxMessageSize, Assert.IsType<int>(oversizedEvent.Payload![1]));
    }

    [Fact]
    public async Task WsTransport_StartAsyncTwiceThrows()
    {
        using var opAmpServer = new OpAmpFakeWebSocketServer(useSmallPackets: false);
        using var wsTransport = CreateTransport(opAmpServer.Endpoint, new FrameProcessor());

        await wsTransport.StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => wsTransport.StartAsync(CancellationToken.None));

        await wsTransport.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WsTransport_StartAsync_WhenCanceled_ThrowsAndTransitionsToPermanentlyFailed()
    {
        // Use a URI that accepts the TCP connection but never completes the WebSocket handshake
        // so that ConnectAsync is still in-flight when we cancel.
        var tcpListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        var acceptTask = Task.CompletedTask;

        try
        {
            tcpListener.Start();
            var port = ((System.Net.IPEndPoint)tcpListener.LocalEndpoint).Port;
            var hangingUri = new Uri($"ws://127.0.0.1:{port}");

            // Accept the TCP connection to prevent an immediate connection-refused, then do nothing
            // so the WebSocket handshake never completes. Dispose the accepted client promptly
            // to avoid unobserved-task noise when the listener stops.
            acceptTask = tcpListener.AcceptTcpClientAsync().ContinueWith(
                t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        t.Result.Dispose();
                    }
                },
                TaskContinuationOptions.ExecuteSynchronously);

            var settings = new OpAmpClientSettings
            {
                ConnectionType = ConnectionType.WebSocket,
                ServerUrl = hangingUri,
            };
            using var wsTransport = new WsTransport(settings, new FrameProcessor());

            using var cts = new CancellationTokenSource();
            var startTask = wsTransport.StartAsync(cts.Token);
            cts.Cancel();

            // .NET Framework throws WebSocketException instead of OperationCanceledException on cancellation.
            var startException = await Record.ExceptionAsync(() => startTask);
            Assert.True(
                startException is OperationCanceledException or WebSocketException,
                $"Expected OperationCanceledException or WebSocketException, but got: {startException?.GetType().Name ?? "null"}");

            // After a canceled start the transport is permanently failed; a second call must
            // throw InvalidOperationException (not OperationCanceledException).
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => wsTransport.StartAsync(CancellationToken.None));
            Assert.Contains("permanently failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            tcpListener.Stop();
            await acceptTask;
        }
    }

    [Fact]
    public async Task WsTransport_AfterFailedStart_SecondStartThrowsPermanentlyFailed()
    {
        // Port 1 is reserved/unreachable on all platforms - ConnectAsync fails immediately.
        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = new Uri("ws://127.0.0.1:1"),
        };
        using var wsTransport = new WsTransport(settings, new FrameProcessor());

        // First call: ConnectAsync fails (connection refused or similar).
        await Assert.ThrowsAnyAsync<Exception>(() => wsTransport.StartAsync(CancellationToken.None));

        // Second call: must throw InvalidOperationException with the "permanently failed" message,
        // not the "already started" message.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => wsTransport.StartAsync(CancellationToken.None));
        Assert.Contains("permanently failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WsTransport_MethodsThrowAfterDispose()
    {
        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = new Uri("ws://localhost:1234"),
        };

        var wsTransport = new WsTransport(settings, new FrameProcessor());
        wsTransport.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => wsTransport.StartAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => wsTransport.StopAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None));
    }

    [Fact]
    public async Task WsTransport_StopCompletesWhenReceiveIsPending()
    {
        var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => Task.CompletedTask);

        var wsTransport = CreateTransport(opAmpServer.Endpoint, new FrameProcessor());

        try
        {
            await wsTransport.StartAsync(CancellationToken.None);

            var stopTask = wsTransport.StopAsync(CancellationToken.None);
            var timeout = TimeSpan.FromSeconds(10);

#if NET
            await stopTask.WaitAsync(timeout);
#else
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(stopTask, Task.Delay(timeout, cts.Token));
            Assert.Same(stopTask, completedTask);
            await stopTask;
#endif

            Assert.True(opAmpServer.TryGetClientCloseStatus(timeout, out var closeStatus), "The server did not observe the client close frame.");
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
        }
        finally
        {
            wsTransport.Dispose();

            try
            {
                opAmpServer.Dispose();
            }
            catch (ApplicationException)
            {
                // Ignore exceptions from the server when the client has already closed the connection
            }
        }
    }

    [Fact]
    public async Task WsTransport_DisposeCompletesWithoutStopWhenReceiveIsPending()
    {
        var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) => Task.CompletedTask);

        // No `using` here so we can call Dispose in a separate task while the transport is still active.
        var wsTransport = CreateTransport(opAmpServer.Endpoint, new FrameProcessor());
        await wsTransport.StartAsync(CancellationToken.None);

        var disposeTask = Task.Run(wsTransport.Dispose);
        var timeout = TimeSpan.FromSeconds(10);

        try
        {
#if NET
            await disposeTask.WaitAsync(timeout);
#else
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(disposeTask, Task.Delay(timeout, cts.Token));
            Assert.Same(disposeTask, completedTask);
            await disposeTask;
#endif
        }
        finally
        {
            try
            {
                opAmpServer.Dispose();
            }
            catch (ApplicationException)
            {
                // Ignore exceptions from the server when the client has already closed the connection
            }
        }
    }

    [Fact]
    public async Task WsTransport_DisposeCompletesWithoutStopAfterOutstandingSend()
    {
        var timeout = TimeSpan.FromSeconds(10);

        using var responseBlocked = new ManualResetEventSlim();
        using var requestSeen = new ManualResetEventSlim();

        var opAmpServer = new OpAmpFakeWebSocketServer(
            (frame, socket, token) =>
            {
                requestSeen.Set();
                responseBlocked.Wait(token);
                return Task.CompletedTask;
            });

        using var wsTransport = CreateTransport(opAmpServer.Endpoint, new FrameProcessor());
        await wsTransport.StartAsync(CancellationToken.None);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None);
        Assert.True(requestSeen.Wait(timeout), "The server did not receive the client request.");

        var disposeTask = Task.Run(wsTransport.Dispose);

        try
        {
#if NET
            await disposeTask.WaitAsync(timeout);
#else
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(disposeTask, Task.Delay(timeout, cts.Token));
            Assert.Same(disposeTask, completedTask);
            await disposeTask;
#endif
        }
        finally
        {
            responseBlocked.Set();

            try
            {
                opAmpServer.Dispose();
            }
            catch (ApplicationException)
            {
                // Ignore exceptions from the server when the client has already closed the connection
            }
        }
    }

    [Fact]
    public void WsReceiver_StartTwiceThrows()
    {
        using var ws = new ClientWebSocket();
        var receiver = new WsReceiver(ws, new FrameProcessor());

        try
        {
            receiver.Start();

            Assert.Throws<InvalidOperationException>(() => receiver.Start());
        }
        finally
        {
            receiver.Dispose();
        }
    }

    [Fact]
    public void WsReceiver_DisposeBeforeStartIsHarmless()
    {
        using var ws = new ClientWebSocket();
        var receiver = new WsReceiver(ws, new FrameProcessor());

        receiver.Dispose();
        receiver.Dispose();
    }

    [Fact]
    public void WsTransport_ThrowsWhenClientWebSocketFactoryReturnsNull()
    {
        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = new Uri("ws://localhost:1234"),
            ClientWebSocketFactory = () => null!,
        };

        var frameProcessor = new FrameProcessor();

        var exception = Assert.Throws<InvalidOperationException>(() => new WsTransport(settings, frameProcessor));

        Assert.Equal("ClientWebSocketFactory returned null. The factory must return a new, unconnected ClientWebSocket instance.", exception.Message);
    }

    private static WsTransport CreateTransport(Uri endpoint, FrameProcessor frameProcessor)
    {
        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = endpoint,
        };

        return new WsTransport(settings, frameProcessor);
    }

    private static async Task SendMessageInChunksAsync(WebSocket socket, ArraySegment<byte> message, WebSocketMessageType messageType, int chunkSize, CancellationToken token)
    {
        var offset = 0;

        while (offset < message.Count)
        {
            var count = Math.Min(chunkSize, message.Count - offset);
            var segment = Slice(message, offset, count);
            var endOfMessage = (offset + count) == message.Count;

            await socket.SendAsync(segment, messageType, endOfMessage, token).ConfigureAwait(false);

            offset += count;
        }
    }

    private static ArraySegment<byte> Slice(ArraySegment<byte> segment, int offset, int count) =>
#if NET
        new(segment.Array!, segment.Offset + offset, count);
#else
        new(segment.Array, segment.Offset + offset, count);
#endif

    private static async Task<EventWrittenEventArgs> WaitForEventAsync(InMemoryEventListener eventListener, string eventName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            while (eventListener.Events.TryDequeue(out var candidate))
            {
                if (candidate.EventName == eventName)
                {
                    return candidate;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for event '{eventName}'.");
    }
}
