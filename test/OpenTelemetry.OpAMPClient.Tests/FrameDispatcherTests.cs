// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.OpAMPClient.Data;
using OpenTelemetry.OpAMPClient.Services.Internal;
using OpenTelemetry.OpAMPClient.Settings;
using OpenTelemetry.OpAMPClient.Tests.Mocks;
using Xunit;

namespace OpenTelemetry.OpAMPClient.Tests;

public class FrameDispatcherTests
{
    [Fact]
    public async Task FrameDispatcher_IsThreadSafe_WhenDispatchingConcurrently()
    {
        var transport = new MockTransport();
        var settings = new OpAMPSettings();
        var dispatcher = new FrameDispatcher(transport, settings);

        var healthReport = new HealthReport
        {
            StartTime = 0, // Use 0 for simplicity in tests
            StatusTime = 0, // Use 0 for simplicity in tests
            DetailedStatus = new HealthStatus
            {
                IsHealthy = true,
                Status = "OK",
            },
        };

        // Simulate concurrent dispatches
        var taskCount = 20; // Number of concurrent tasks
        await Parallel.ForEachAsync(Enumerable.Range(0, taskCount), async (i, token) =>
        {
            if (i % 2 == 0)
            {
                await dispatcher.DispatchIdentificationFrameAsync(token);
            }
            else
            {
                await dispatcher.DispatchHeartbeatAsync(healthReport, token);
            }
        });

        // Assert that all messages were sent without exceptions
        Assert.Equal(taskCount, transport.Messages.Count);

        // Assert that sequence numbers are from 1 to N (no duplicates, no gaps)
        var sequenceNumbers = transport.Messages
            .Select(m => m.SequenceNum)
            .ToArray();
        Assert.Equal(Enumerable.Range(1, taskCount).Select(i => (ulong)i), sequenceNumbers);
    }
}
