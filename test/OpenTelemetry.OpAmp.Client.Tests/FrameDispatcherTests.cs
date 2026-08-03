// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class FrameDispatcherTests
{
    [Fact]
    public async Task FrameDispatcher_IsThreadSafe_WhenDispatchingConcurrently()
    {
        // Simulate concurrent dispatches
        var taskCount = 20; // Number of concurrent tasks

        var transport = new MockTransport(taskCount);
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        var dispatcher = new OpAmpPipe(settings, processor, transport);
        var mockAnswer = new ServerToAgent().ToByteArray();

        await Parallel.ForEachAsync(Enumerable.Range(0, taskCount), async (i, token) =>
        {
            dispatcher.AppendMessage(fb => fb.AddAgentDescription());
            processor.OnServerFrame(new ReadOnlySequence<byte>(mockAnswer));
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
#endif
