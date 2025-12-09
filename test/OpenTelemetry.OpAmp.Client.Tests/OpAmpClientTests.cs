// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using Xunit;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpClientTests
{
    [Fact]
    public async Task OpAmpClient_SubscribeAndUnsubscribe()
    {
        using var opAmpServer = new OpAmpFakeHttpServer(false);
        var opAmpEndpoint = opAmpServer.Endpoint;

        using var mockListener = new MockListener();

        using var client = new OpAmpClient(o =>
        {
            o.ServerUrl = opAmpEndpoint;
            o.Heartbeat.Interval = TimeSpan.FromSeconds(1);
        });
        client.Subscribe(mockListener);

        await client.StartAsync();

        // We don't currently have a direct way to send a message from the client to the server to trigger a response, so
        // this depends on the heartbeat messages from the server to the client.

        mockListener.WaitForMessages(TimeSpan.FromSeconds(2)); // Wait for the initial identification message response
        mockListener.WaitForMessages(TimeSpan.FromSeconds(2)); // Wait for the heartbeat response

        client.Unsubscribe(mockListener);

        mockListener.WaitForMessages(TimeSpan.FromSeconds(3)); // Wait to verify no more messages arrive after unsubscribing.

        var clientReceivedFrames = mockListener.Messages;
        Assert.Equal(2, clientReceivedFrames.Count); // Expect only two messages received

        var receivedTextData = clientReceivedFrames.First().CustomMessage.Data.ToStringUtf8();
        Assert.StartsWith("This is a mock server frame for testing purposes.", receivedTextData);

        await client.StopAsync();
    }
}
