// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.OpAmp.Client.Transport.WebSocket;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class WsTransportTest
{
    [Fact]
    public async Task WsTransport_SendReceiveCommunication()
    {
        // Arrange
        using var opAmpServer = new OpAmpFakeWebSocketServer();
        var opAmpEndpoint = opAmpServer.Endpoint;

        var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        using var wsTransport = new WsTransport(opAmpEndpoint, frameProcessor);
        await wsTransport.StartAsync(CancellationToken.None);

        var uid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
        var frame = new AgentToServer() { InstanceUid = uid };

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await wsTransport.SendAsync(frame, cts.Token);

        mockListener.WaitForMessages(TimeSpan.FromSeconds(30));

        await wsTransport.StopAsync(cts.Token);

        cts.Cancel();

        // Assert
        var serverReceivedFrames = opAmpServer.GetFrames();
        var clientReceivedFrames = mockListener.Messages;

        Assert.Single(serverReceivedFrames);
        Assert.Equal(uid, serverReceivedFrames.First().InstanceUid);

        Assert.Single(clientReceivedFrames);
        Assert.Equal("Response from OpAmpFakeWebSocketServer", clientReceivedFrames.First().CustomMessage.Data.ToStringUtf8());
    }
}

#endif

