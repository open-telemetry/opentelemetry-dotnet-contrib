// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Messages;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpHttpPipeTests : OpAmpPipeTests
{
    [Fact]
    public async Task OpAmpPipe_DoesNotReleaseNewerSend_WhenPublicListenerThrowsDuringHttpResponseProcessing()
    {
        var processor = new FrameProcessor();
        var serverFrame = new ServerToAgent
        {
            CustomMessage = new CustomMessage
            {
                Data = ByteString.CopyFromUtf8("response"),
                Type = "Utf8String",
            },
        }.ToByteArray();

        using var transport = this.GetTransport(
            () => processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame)));
        var settings = new OpAmpClientSettings();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        processor.Subscribe(new AppendHeartbeatAndThrowListener(pipe));

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(2);

        AppendHeartbeat(pipe);

        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.WaitForMessagesAsync(3, TimeSpan.FromMilliseconds(250)));

        Assert.Equal(2, transport.Messages.Count);
    }

    [Fact]
    public async Task OpAmpPipe_FlushAsyncWaitsForResponseQueuedDataToBeSent()
    {
        var processor = new FrameProcessor();
        var responseWithCustomMessage = new ServerToAgent
        {
            CustomMessage = new CustomMessage
            {
                Data = ByteString.CopyFromUtf8("response"),
                Type = "Utf8String",
            },
        }.ToByteArray();
        var emptyResponse = new ServerToAgent().ToByteArray();

        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        processor.Subscribe(new AppendHeartbeatListener(pipe));

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        AppendHeartbeat(pipe);
        var flushTask = pipe.FlushAsync();

        transport.CompleteNextSend(
            () => processor.OnServerFrame(new ReadOnlySequence<byte>(responseWithCustomMessage)));

        await transport.WaitForMessagesAsync(2);
        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.WaitForMessagesAsync(3, TimeSpan.FromMilliseconds(250)));

        transport.CompleteNextSend(
            () => processor.OnServerFrame(new ReadOnlySequence<byte>(emptyResponse)));

        await transport.WaitForMessagesAsync(3);
        Assert.False(flushTask.IsCompleted);

        transport.CompleteNextSend(
            () => processor.OnServerFrame(new ReadOnlySequence<byte>(emptyResponse)));

#if NET
        await flushTask.WaitAsync(TimeSpan.FromSeconds(5));
#else
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

        if (await Task.WhenAny(flushTask, timeoutTask).ConfigureAwait(true)
            == timeoutTask)
        {
            throw new TimeoutException("Flush did not complete within 5 seconds.");
        }
#endif

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL, 3UL], messages.Select(m => m.SequenceNum).ToArray());
        Assert.NotNull(messages[0].AgentDescription);
        Assert.NotNull(messages[1].Health);
        Assert.NotNull(messages[2].Health);
    }

    private sealed class AppendHeartbeatListener : IOpAmpListener<CustomMessageMessage>
    {
        private readonly OpAmpPipe pipe;

        public AppendHeartbeatListener(OpAmpPipe pipe)
        {
            this.pipe = pipe;
        }

        public void HandleMessage(CustomMessageMessage message)
            => AppendHeartbeat(this.pipe);
    }

    private sealed class AppendHeartbeatAndThrowListener : IOpAmpListener<CustomMessageMessage>
    {
        private readonly OpAmpPipe pipe;

        public AppendHeartbeatAndThrowListener(OpAmpPipe pipe)
        {
            this.pipe = pipe;
        }

        public void HandleMessage(CustomMessageMessage message)
        {
            AppendHeartbeat(this.pipe);
            throw new InvalidOperationException("listener failed");
        }
    }

    internal override MockControlledTransport GetTransport(Action? firstSendCallback = null) => new MockControlledHttpTransport(firstSendCallback);
}
