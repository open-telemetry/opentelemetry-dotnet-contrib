// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Messages;
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
        using var transport = new MockControlledTransport();
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
    public async Task HeartbeatService_EmitsHeartbeats()
    {
        const int messagesCount = 3;
        const int intervalMs = 50;

        var settings = new OpAmpClientSettings
        {
            Heartbeat = new HeartbeatSettings
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs), // Set a short interval for testing
            },
        };

        var processor = new FrameProcessor();
        using var transport = new MockControlledTransport();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        using var service = new HeartbeatService(pipe, processor);

        var serverFrame = new ServerToAgent().ToByteArray();

        service.Configure(settings);
        service.Start();

        try
        {
            for (var i = 1; i <= messagesCount; i++)
            {
                await transport.WaitForMessagesAsync(i);
                transport.CompleteNextSend();
                processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));
            }
        }
        finally
        {
            service.Stop();
        }

        var messages = transport.Messages;
        Assert.True(messages.Count >= messagesCount, $"Expecting at least {messagesCount} heartbeats, got {messages.Count}.");
        Assert.All(messages, message => Assert.NotNull(message.Health));
    }
}
