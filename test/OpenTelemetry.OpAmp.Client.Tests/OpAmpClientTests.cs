// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
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
            o.Heartbeat.IsEnabled = false;
        });
        client.Subscribe(mockListener);

        await client.StartAsync();

        // We don't currently have a direct way to send a message from the client to the server to trigger a response, so
        // this depends on the heartbeat messages from the server to the client.

        // Wait for the initial identification message response
        mockListener.WaitForMessages(TimeSpan.FromSeconds(1));

        Assert.Single(mockListener.Messages);

        await client.SendHeartbeatAsync(new HealthReport
        {
            StartTime = GetCurrentTimeInNanoseconds(),
            StatusTime = GetCurrentTimeInNanoseconds(),
            IsHealthy = true,
            Status = "OK",
        });

        // Wait for the heartbeat response
        mockListener.WaitForMessages(TimeSpan.FromSeconds(1));

        Assert.Equal(2, mockListener.Messages.Count);

        client.Unsubscribe(mockListener);

        await client.SendHeartbeatAsync(new HealthReport
        {
            StartTime = GetCurrentTimeInNanoseconds(),
            StatusTime = GetCurrentTimeInNanoseconds(),
            IsHealthy = true,
            Status = "OK",
        });

        mockListener.WaitForMessages(TimeSpan.FromSeconds(1));

        var serverFrames = opAmpServer.GetFrames();

        // We should have received 3 frames on the server: identification, heartbeat 1, heartbeat 2
        // The client should have received 2 messages before we unsubscribed.
        Assert.Equal(2, mockListener.Messages.Count);
        Assert.Equal(3, serverFrames.Count);

        await client.StopAsync();

        static ulong GetCurrentTimeInNanoseconds()
        {
            return (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000; // Convert to nanoseconds
        }
    }
}
