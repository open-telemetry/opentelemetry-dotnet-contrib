// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class PlainHttpTransportTest
{
    [Fact]
    public async Task PlainHttpTransport_SendReceiveCommunication()
    {
        // Arrange
        using var opAmpServer = new OpAmpFakeHttpServer();
        var opAmpEndpoint = opAmpServer.Endpoint;

        var mockListener = new MockListener();
        var frameProcessor = new FrameProcessor();
        frameProcessor.Subscribe(mockListener);

        var httpTransport = new PlainHttpTransport(opAmpEndpoint, frameProcessor);

        var uid = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
        var frame = new AgentToServer() { InstanceUid = uid };

        // Act
        await httpTransport.SendAsync(frame, CancellationToken.None);

        // Assert
        var serverReceivedFrames = opAmpServer.GetFrames();
        var clientReceivedFrames = mockListener.Messages;

        Assert.Single(serverReceivedFrames);
        Assert.Equal(uid, serverReceivedFrames.First().InstanceUid);

        Assert.Single(clientReceivedFrames);
        Assert.Equal("Response from OpAmpFakeHttpServer", clientReceivedFrames.First().CustomMessage.Data.ToStringUtf8());
    }
}
