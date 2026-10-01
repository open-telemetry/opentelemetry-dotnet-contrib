// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;

namespace OpenTelemetry.OpAmp.Client.Tests;

public class OpAmpWsPipeTests : OpAmpPipeTests
{
    [Fact]
    public async Task OpAmpPipe_DoesNotRecursivelyDrainSynchronouslyCompletedSends()
    {
        const int customMessageCount = 64;

        var transport = new InlineCompletionTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendIdentification(pipe);
        Assert.True(
            transport.WaitForFirstSend(TimeSpan.FromSeconds(5)),
            "The first send did not start within 5 seconds.");

        for (var i = 0; i < customMessageCount; i++)
        {
            AppendCustomMessage(pipe, i);
        }

        transport.CompleteFirstSend();

        using var flushCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await pipe.FlushAsync(flushCancellation.Token);

        Assert.True(
            transport.StackDepthGrowth < 16,
            $"The send call stack grew by {transport.StackDepthGrowth} frames while draining the queue.");
        Assert.Equal(customMessageCount + 1, transport.SendCount);
    }

    [Fact]
    public void OpAmpPipe_UsesServerAssignedInstanceUid_AssignedWhileWaitingForTheSocket()
    {
        using var transport = new WaitingForSocketTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var newInstanceUid = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray());
        var serverFrame = new ServerToAgent
        {
            AgentIdentification = new AgentIdentification { NewInstanceUid = newInstanceUid },
        }.ToByteArray();

        AppendIdentification(pipe);
        Assert.True(
            transport.WaitForSendStarted(TimeSpan.FromSeconds(5)),
            "The send did not start within 5 seconds.");

        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));
        transport.FreeSocket();

        Assert.True(
            transport.WaitForSent(TimeSpan.FromSeconds(5)),
            "The message was not sent within 5 seconds.");
        Assert.Equal(newInstanceUid, transport.SentInstanceUid);
    }

    internal override MockControlledTransport GetTransport(Action? firstSendCallback = null) => new MockControlledWsTransport(firstSendCallback);

    private sealed class InlineCompletionTransport : IOpAmpTransport, IDisposable
    {
        // Hold the first send while the queue fills; every later send completes synchronously.
        private readonly TaskCompletionSource<bool> firstSendCompletion = new();
        private readonly ManualResetEventSlim firstSendStarted = new();
        private readonly Lock stackDepthLock = new();
        private int maximumStackDepth;
        private int minimumStackDepth = int.MaxValue;
        private int sendCount;

        public bool RequiresResponseBeforeNextSend => false;

        public int SendCount => Volatile.Read(ref this.sendCount);

        public int StackDepthGrowth
        {
            get
            {
                lock (this.stackDepthLock)
                {
                    return this.maximumStackDepth - this.minimumStackDepth;
                }
            }
        }

        public Task SendAsync<T>(T message, CancellationToken token, Action<T>? beforeSerialize = null)
            where T : IMessage<T>
        {
            if (Interlocked.Increment(ref this.sendCount) == 1)
            {
                this.firstSendStarted.Set();
                return this.firstSendCompletion.Task;
            }

            var stackDepth = new StackTrace().FrameCount;
            lock (this.stackDepthLock)
            {
                this.minimumStackDepth = Math.Min(this.minimumStackDepth, stackDepth);
                this.maximumStackDepth = Math.Max(this.maximumStackDepth, stackDepth);
            }

            return Task.CompletedTask;
        }

        public bool WaitForFirstSend(TimeSpan timeout) => this.firstSendStarted.Wait(timeout);

        public void CompleteFirstSend() => this.firstSendCompletion.SetResult(true);

        public void Dispose() => this.firstSendStarted.Dispose();
    }

    private sealed class WaitingForSocketTransport : IOpAmpTransport, IDisposable
    {
        // Like WsTransmitter waiting for its send lock, a send waits for the socket before it serializes the message.
        private readonly TaskCompletionSource<bool> socketFree = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim sendStarted = new();
        private readonly ManualResetEventSlim sent = new();

        public bool RequiresResponseBeforeNextSend => false;

        public ByteString? SentInstanceUid { get; private set; }

        public async Task SendAsync<T>(T message, CancellationToken token, Action<T>? beforeSerialize = null)
            where T : IMessage<T>
        {
            this.sendStarted.Set();
            await this.socketFree.Task.ConfigureAwait(false);

            beforeSerialize?.Invoke(message);
            this.SentInstanceUid = ((AgentToServer)(object)message).InstanceUid;
            this.sent.Set();
        }

        public bool WaitForSendStarted(TimeSpan timeout) => this.sendStarted.Wait(timeout);

        public bool WaitForSent(TimeSpan timeout) => this.sent.Wait(timeout);

        public void FreeSocket() => this.socketFree.SetResult(true);

        public void Dispose()
        {
            this.sendStarted.Dispose();
            this.sent.Dispose();
        }
    }
}
