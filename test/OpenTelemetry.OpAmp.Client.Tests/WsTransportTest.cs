// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Net.WebSockets;
using System.Text;
using Google.Protobuf;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.Tests;

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

        var frames = Assert.Single(serverReceivedFrames);
        Assert.Equal(mockFrame.Uid, frames.InstanceUid);

        Assert.Single(clientReceivedFrames);
        Assert.StartsWith("This is a mock server frame for testing purposes.", receivedTextData);
    }

    [Fact]
    public async Task WsTransport_CallsBeforeSerializeBeforeSendingTheMessage()
    {
        using var opAmpServer = new OpAmpFakeWebSocketServer(useSmallPackets: true);

        using var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = opAmpServer.Endpoint,
        };
        using var wsTransport = new WsTransport(settings, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);

        var instanceUid = ByteString.CopyFrom([.. Enumerable.Range(1, 16).Select(i => (byte)i)]);
        var mockFrame = FrameGenerator.GenerateMockAgentFrame();

        await wsTransport.SendAsync(mockFrame.Frame, CancellationToken.None, message => message.InstanceUid = instanceUid);

        mockListener.WaitForMessages(TimeSpan.FromSeconds(30));
        await wsTransport.StopAsync(CancellationToken.None);

        var frame = Assert.Single(opAmpServer.GetFrames());
        Assert.Equal(instanceUid, frame.InstanceUid);
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

        Assert.True(opAmpServer.TryGetRequestHeaders(TimeSpan.FromSeconds(5), out var requestHeaders));
        Assert.Equal("ConfiguredByFactory", requestHeaders?["X-Test-Header"]);
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

        Assert.True(thresholdReached.Wait(timeout, TestContext.Current.CancellationToken), "The server did not send enough bytes to exceed the transport limit.");
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
        await wsTransport.StartAsync(TestContext.Current.CancellationToken);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, TestContext.Current.CancellationToken);

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

        await eventListener.WaitForEventAsync(
            e => e.EventName == nameof(OpAmpClientEventSource.InvalidWsFrame)
                && e.Payload![0] is string errorMessage
                && string.Equals(errorMessage, "Invalid OpAmp WebSocket header: 1.", StringComparison.Ordinal),
            $"{nameof(OpAmpClientEventSource.InvalidWsFrame)} for header 1",
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WsTransport_ContinuesAfterInvalidBinaryFrame()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        var invalidFrame = new ArraySegment<byte>([0x02]);
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

        await eventListener.WaitForEventAsync(
            e => e.EventName == nameof(OpAmpClientEventSource.InvalidWsFrame)
                && e.Payload![0] is string errorMessage
                && string.Equals(errorMessage, "Invalid OpAmp WebSocket header: 2.", StringComparison.Ordinal),
            $"{nameof(OpAmpClientEventSource.InvalidWsFrame)} for header 2",
            timeout);

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

        // SendAsync can race with the receiver aborting the socket after detecting the oversized response.
        // On .NET Framework this may surface as a WebSocketException (Aborted state) or OperationCanceledException.
        var sendException = await Record.ExceptionAsync(
            () => wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None));
        Assert.True(
            sendException is null or WebSocketException or OperationCanceledException,
            $"Unexpected exception from SendAsync: {sendException}");

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

        await eventListener.WaitForEventAsync(
            e => e.EventName == nameof(OpAmpClientEventSource.InvalidWsFrame)
                && e.Payload![0] is string errorMessage
                && string.Equals(errorMessage, "Invalid OpAmp WebSocket header: 110.", StringComparison.Ordinal),
            $"{nameof(OpAmpClientEventSource.InvalidWsFrame)} for header 110",
            TimeSpan.FromSeconds(5));
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

        // SendAsync can race with the receiver aborting the socket after detecting the oversized response.
        // On .NET Framework this may surface as a WebSocketException (Aborted state) or OperationCanceledException.
        var sendException = await Record.ExceptionAsync(
            () => wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, CancellationToken.None));
        Assert.True(
            sendException is null or WebSocketException or OperationCanceledException,
            $"Unexpected exception from SendAsync: {sendException}");

        Assert.True(opAmpServer.TryGetClientCloseStatus(timeout, out var closeStatus));
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, closeStatus);

        var oversizedEvent = await eventListener.WaitForEventAsync(
            e => e.EventName == nameof(OpAmpClientEventSource.OversizedWebSocketMessage)
                && e.Payload![0] is int minimumBytes
                && minimumBytes == TransportConstants.MaxMessageSize + 1
                && e.Payload[1] is int limitBytes
                && limitBytes == TransportConstants.MaxMessageSize,
            $"{nameof(OpAmpClientEventSource.OversizedWebSocketMessage)} for {TransportConstants.MaxMessageSize + 1} bytes",
            timeout);

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
            acceptTask =
#if NET
                tcpListener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask()
#else
                tcpListener.AcceptTcpClientAsync()
#endif
                .ContinueWith(
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
            await stopTask.WaitAsync(timeout, TestContext.Current.CancellationToken);
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

        var disposeTask = Task.Run(wsTransport.Dispose, TestContext.Current.CancellationToken);
        var timeout = TimeSpan.FromSeconds(10);

        try
        {
#if NET
            await disposeTask.WaitAsync(timeout, TestContext.Current.CancellationToken);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WsReceiver_StartDoesNotReceiveSynchronouslyAvailableStreamOnCallingThread(bool malformed)
    {
        const int frameCount = 10;

        // OpAMP header 0, followed by either ReportFullState flags or truncated protobuf.
        byte[] payload = malformed ? [0x00, 0xff] : [0x00, 0x30, 0x01];
        using var socket = new SynchronousReceiveWebSocket(new ArraySegment<byte>(payload), frameCount);
        using var sendLock = new SemaphoreSlim(1, 1);
        var listener = new RecordingFlagsMessageListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(listener);
        using var receiver = new WsReceiver(socket, frameProcessor, sendLock);
        var token = TestContext.Current.CancellationToken;
        Exception? startException = null;

        // A dedicated thread cannot also execute the queued receive loop. Using a test runner
        // or thread-pool thread here could reuse that thread after Start returns.
        var startThread = new Thread(() =>
        {
            try
            {
                receiver.Start(token);
            }
            catch (Exception ex)
            {
                startException = ex;
            }
        });

        startThread.Start();
        startThread.Join();
        Assert.Null(startException);

        // Wait for every receive before disposing, including when malformed frames never reach
        // the listener. Dispose then joins the loop so the final frame finishes processing.
        socket.FramesReceived.Wait(token);
        receiver.Dispose();

        Assert.Equal(frameCount, socket.ReceiveCount);
        Assert.NotNull(socket.ReceiveThread);
        Assert.NotSame(startThread, socket.ReceiveThread);
        Assert.Equal(malformed ? 0 : frameCount, listener.MessageCount);
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
        await wsTransport.StartAsync(TestContext.Current.CancellationToken);
        await wsTransport.SendAsync(FrameGenerator.GenerateMockAgentFrame().Frame, TestContext.Current.CancellationToken);
        Assert.True(requestSeen.Wait(timeout, TestContext.Current.CancellationToken), "The server did not receive the client request.");

        var disposeTask = Task.Run(wsTransport.Dispose, TestContext.Current.CancellationToken);

        try
        {
#if NET
            await disposeTask.WaitAsync(timeout, TestContext.Current.CancellationToken);
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
        using var sendLock = new SemaphoreSlim(1, 1);
        var receiver = new WsReceiver(ws, new FrameProcessor(), sendLock);

        try
        {
            receiver.Start(TestContext.Current.CancellationToken);

            Assert.Throws<InvalidOperationException>(() => receiver.Start(TestContext.Current.CancellationToken));
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
        using var sendLock = new SemaphoreSlim(1, 1);
        var receiver = new WsReceiver(ws, new FrameProcessor(), sendLock);

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

#if NET
    [Theory]
    [InlineData(false, false, false)] // default client blocks redirects
    [InlineData(true, false, true)] // default client follows redirects when allowed
    [InlineData(false, true, true)] // custom factory owns redirect behavior
    public async Task WebSocket_Redirect(bool allowRedirects, bool customFactory, bool expectFollowed)
    {
        using var targetReached = new ManualResetEventSlim();
        using var target = TestWebSocketServer.RunServer(
            (WebSocket _) =>
            {
                targetReached.Set();
                return Task.CompletedTask;
            },
            out var targetHost,
            out var targetPort);
        using var redirector = RunRedirector(new Uri($"http://{targetHost}:{targetPort}/"), out var redirectorUri);

        var settings = new OpAmpClientSettings
        {
            ConnectionType = ConnectionType.WebSocket,
            ServerUrl = new UriBuilder(redirectorUri) { Scheme = "ws" }.Uri,
            AllowRedirects = allowRedirects,
        };
        if (customFactory)
        {
            settings.ClientWebSocketFactory = () => new ClientWebSocket();
        }

        using var transport = new WsTransport(settings, new FrameProcessor());

        if (expectFollowed)
        {
            await transport.StartAsync(TestContext.Current.CancellationToken);
            Assert.True(targetReached.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), "Target server was not reached.");
        }
        else
        {
            // The handshake fails on the redirect response, so the target is never contacted.
            await Assert.ThrowsAsync<WebSocketException>(() => transport.StartAsync(TestContext.Current.CancellationToken));
            Assert.False(targetReached.IsSet);
        }
    }
#endif

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

    private static IDisposable RunRedirector(Uri location, out Uri address)
        => TestHttpServer.RunServer(
            ctx =>
            {
                ctx.Response.StatusCode = 307;
                ctx.Response.RedirectLocation = location.ToString();
                ctx.Response.Close();
            },
            out address);

    private sealed class RecordingFlagsMessageListener : IOpAmpListener<FlagsMessage>
    {
        public int MessageCount { get; private set; }

        public void HandleMessage(FlagsMessage message)
        {
            this.MessageCount++;
        }
    }
}
