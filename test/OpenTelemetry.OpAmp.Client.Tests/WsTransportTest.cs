// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
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
        using var wsTransport = new WsTransport(opAmpEndpoint, frameProcessor);
        await wsTransport.StartAsync(cts.Token);

        // Send only small packets, currently sending large package is not supported in WsTransport
        var mockFrame = FrameGenerator.GenerateMockAgentFrame(useSmallPackets: true);

        // Act
        await wsTransport.SendAsync(mockFrame.Frame, cts.Token);

        mockListener.WaitForMessages(TimeSpan.FromSeconds(30));

        await wsTransport.StopAsync(cts.Token);

        cts.Cancel();

        // Assert
        var serverReceivedFrames = opAmpServer.GetFrames();
        var clientReceivedFrames = mockListener.Messages;
        var receivedTextData = clientReceivedFrames.First().CustomMessage.Data.ToStringUtf8();

        Assert.Single(serverReceivedFrames);
        Assert.Equal(mockFrame.Uid, serverReceivedFrames.First().InstanceUid);

        Assert.Single(clientReceivedFrames);
        Assert.StartsWith("This is a mock server frame for testing purposes.", receivedTextData);
    }
}
