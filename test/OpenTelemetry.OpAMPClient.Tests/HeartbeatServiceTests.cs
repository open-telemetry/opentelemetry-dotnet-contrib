// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Services;
using OpenTelemetry.OpAMPClient.Settings;
using OpenTelemetry.OpAMPClient.Tests.Mocks;
using Xunit;

namespace OpenTelemetry.OpAMPClient.Tests;

public class HeartbeatServiceTests
{
    [Fact]
    public async Task HeartbeatService_EmitsFiveHeartbeats()
    {
        const int messagesCount = 5;
        const int intervalMs = 300;

        var delay = messagesCount * intervalMs; // No buffer is needed as the service starts with the heartbeat immediately
        var settings = new OpAMPSettings
        {
            HeartbeatSettings = new HeartbeatSettings
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs), // Set a short interval for testing
                InitialStatus = "Healthy",
                ShouldWaitForFirstStatus = false,
            },
        };

        var transport = new MockTransport();
        var dispatcher = new FrameDispatcher(transport, settings);
        var service = new HeartbeatService(dispatcher);

        service.Configure(settings);
        service.Start();

        // Wait for the service to finish
        await Task.Delay(delay);

        service.Stop();

        var count = transport.Messages.Count;
        Assert.True(count >= messagesCount, $"Expecting at least {messagesCount} heartbeats, got {count}.");
    }
}
