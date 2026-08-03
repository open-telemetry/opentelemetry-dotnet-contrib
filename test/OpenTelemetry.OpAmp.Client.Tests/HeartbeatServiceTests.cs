// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Listeners.Messages;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class HeartbeatServiceTests
{
    [Theory]
    [InlineData(ulong.MaxValue)] // far beyond TimeSpan.MaxValue
    [InlineData(922_337_203_686ul)] // just above TimeSpan.MaxValue.TotalSeconds (~922337203685)
    public void HeartbeatService_HandleMessage_OversizedInterval_DoesNotThrow(ulong intervalSeconds)
    {
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var transport = new MockTransport(expectedCount: 0);
        using var pipe = new OpAmpPipe(settings, processor, transport);
        using var service = new HeartbeatService(pipe, processor);
        service.Configure(settings);

        var message = new ConnectionSettingsMessage(new ConnectionSettingsOffers
        {
            Opamp = new OpAMPConnectionSettings { HeartbeatIntervalSeconds = intervalSeconds },
        });

        // Must not throw OverflowException.
        service.HandleMessage(message);
    }

    [Fact]
    public void HeartbeatService_EmitsHeartbeats()
    {
        const int messagesCount = 3;
        const int intervalMs = 300;

        var settings = new OpAmpClientSettings
        {
            Heartbeat = new HeartbeatSettings
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs), // Set a short interval for testing
            },
        };

        var processor = new FrameProcessor();
        using var transport = new MockTransport(messagesCount);
        using var pipe = new OpAmpPipe(settings, processor, transport);
        using var service = new HeartbeatService(pipe, processor);

        service.Configure(settings);
        service.Start();

        transport.WaitForMessages(timeout: TimeSpan.FromSeconds(5));

        service.Stop();

        var count = transport.Messages.Count;
        Assert.True(count >= messagesCount, $"Expecting at least {messagesCount} heartbeats, got {count}.");
    }
}
