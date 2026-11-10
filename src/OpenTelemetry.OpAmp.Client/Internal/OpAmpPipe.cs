// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal.Messages;
using OpenTelemetry.OpAmp.Client.Internal.Transport;
using OpenTelemetry.OpAmp.Client.Internal.Transport.Http;
using OpenTelemetry.OpAmp.Client.Internal.Transport.WebSocket;
using OpenTelemetry.OpAmp.Client.Listeners;
using OpenTelemetry.OpAmp.Client.Settings;

namespace OpenTelemetry.OpAmp.Client.Internal;

internal sealed class OpAmpPipe : IDisposable
{
    private static readonly byte[] EmptyInstanceUid = new byte[16];

    private readonly IOpAmpTransport transport;
    private readonly FrameProcessor processor;
    private readonly int maxPendingCustomMessages;
    private readonly int maxPendingMessageBytes;
    private readonly Lock frameLock = new();
    private readonly CancellationTokenSource tokenSource = new();
    private readonly ServerFrameHandler frameHandler;
    private readonly Action<AgentToServer> applyAssignedInstanceUid;
    private readonly FrameBuilder currentFrame;
    private readonly Queue<PendingFrame> pendingFrames = [];

    // Flush waiters ordered by target. Targets are captured under frameLock from a monotonic counter,
    // so appending keeps the list sorted.
    private readonly LinkedList<FlushWaiter> flushWaiters = new();

    private bool isBusy;
    private bool isDisposed;
    private bool isStopped;
    private bool hasAccumulatedData;
    private long pendingMessageBytes;
    private ByteString? assignedInstanceUid;
    private Task? flushTask;
    private long framesBuilt;
    private long framesCompleted;

    public OpAmpPipe(OpAmpClientSettings settings, FrameProcessor processor)
        : this(settings, processor, ConstructTransport(settings, processor))
    {
    }

    public OpAmpPipe(OpAmpClientSettings settings, FrameProcessor processor, IOpAmpTransport transport)
    {
        this.processor = processor;
        this.transport = transport;
        this.currentFrame = new FrameBuilder(settings);
        this.maxPendingCustomMessages = settings.MaxPendingCustomMessages;
        this.maxPendingMessageBytes = settings.MaxPendingMessageBytes;

        this.frameHandler = new(this.OnServerFrameReceived);
        this.processor.Subscribe(this.frameHandler);
        this.applyAssignedInstanceUid = this.ApplyAssignedInstanceUid;
    }

    public async Task StartAsync(CancellationToken token = default)
    {
        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StartAsync(token)
                .ConfigureAwait(false);
        }

        this.AppendMessage(MessageBuilderHelper.AppendIdentification);

        // Ensure identification is sent.
        await this.FlushAsync(token)
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken token = default)
    {
        // Set the pipe to stopped state. New data should not accumulate.
        lock (this.frameLock)
        {
            this.isStopped = true;
        }

        // Drain all accumulated data.
        await this.FlushAsyncCore(force: true, token)
            .ConfigureAwait(false);

        token.ThrowIfCancellationRequested();

        lock (this.frameLock)
        {
            // Do not use AppendMessage(), it is already locked for closure and does extra flush attempt.
            MessageBuilderHelper.AppendAgentDisconnect(this.currentFrame);

            // Mark the stop frame is available.
            this.hasAccumulatedData = true;
        }

        // Do final flush and send disconnect
        await this.FlushAsyncCore(force: true, token)
            .ConfigureAwait(false);

        if (this.transport is WsTransport wsTransport)
        {
            await wsTransport.StopAsync(token)
                .ConfigureAwait(false);
        }
    }

    public void AppendMessage(Action<IFrameBuilder> messageRequest)
    {
        lock (this.frameLock)
        {
            if (!this.IsAcceptingMessagesLocked())
            {
                return; // Discard any new messages
            }

            // Mark before invoking: the callback may partially modify the builder and then throw,
            // and any retained data must still be counted against the byte limit.
            this.hasAccumulatedData = true;
            messageRequest(this.currentFrame);
        }

        this.TryFlush(this.tokenSource.Token);
    }

    public void AppendCustomMessage(string capability, string type, ReadOnlyMemory<byte> data)
    {
        lock (this.frameLock)
        {
            if (!this.IsAcceptingMessagesLocked())
            {
                return; // Discard any new messages
            }

            if (this.pendingFrames.Count >= this.maxPendingCustomMessages)
            {
                throw new InvalidOperationException(
                    $"The pending message queue has reached its limit of {this.maxPendingCustomMessages} messages.");
            }

            // Accumulated state reports are sent in the same frame, so they count towards the byte
            // limit. The size is measured before the builder is modified so a rejected message
            // leaves those reports intact.
            var availableBytes = this.maxPendingMessageBytes - this.pendingMessageBytes;
            var frameBytes = (long)data.Length;

            // Only measure the accumulated reports when the payload alone fits.
            if (frameBytes <= availableBytes && this.hasAccumulatedData)
            {
                frameBytes += this.currentFrame.CalculateSize();
            }

            if (frameBytes > availableBytes)
            {
                throw new InvalidOperationException(
                    $"The pending message queue would exceed its limit of {this.maxPendingMessageBytes} bytes.");
            }

            MessageBuilderHelper.AppendCustomMessage(this.currentFrame, capability, type, data);

            this.pendingFrames.Enqueue(new PendingFrame(this.currentFrame.Build(), frameBytes));
            this.framesBuilt++;
            this.pendingMessageBytes += frameBytes;
            this.hasAccumulatedData = false;
        }

        this.TryFlush(this.tokenSource.Token);
    }

    public Task FlushAsync(CancellationToken token = default) =>
        this.FlushAsyncCore(force: false, token);

    public void Dispose()
    {
        lock (this.frameLock)
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;
            this.pendingFrames.Clear();
            this.pendingMessageBytes = 0;
            this.CompleteFlushWaitersLocked();
        }

        this.tokenSource.Cancel();
        this.tokenSource.Dispose();

        this.processor.Unsubscribe(this.frameHandler);

        if (this.transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    private static IOpAmpTransport ConstructTransport(OpAmpClientSettings settings, FrameProcessor processor) => settings.ConnectionType switch
    {
        ConnectionType.WebSocket => new WsTransport(settings, processor),
        ConnectionType.Http => new PlainHttpTransport(settings, processor),
        _ => throw new NotSupportedException("Unsupported transport type"),
    };

    private async Task WaitForFlushAsync(LinkedListNode<FlushWaiter> waiterNode, CancellationToken token)
    {
        var flushTask = waiterNode.Value.Completion.Task;

        if (!token.CanBeCanceled)
        {
            await flushTask.ConfigureAwait(false);
            return;
        }

        var cancellationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(
            static state => ((TaskCompletionSource<bool>?)state)?.TrySetResult(true),
            cancellationCompletion);

        if (await Task.WhenAny(flushTask, cancellationCompletion.Task).ConfigureAwait(false) == cancellationCompletion.Task)
        {
            // Do not leave abandoned waiters queued behind a stalled send.
            lock (this.frameLock)
            {
                if (waiterNode.List is not null)
                {
                    this.flushWaiters.Remove(waiterNode);
                }
            }

            token.ThrowIfCancellationRequested();
        }

        await flushTask.ConfigureAwait(false);
    }

    private bool IsAcceptingMessagesLocked()
        => !this.isStopped &&
            !this.isDisposed &&
            !this.tokenSource.IsCancellationRequested;

    private Task FlushAsyncCore(bool force, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        lock (this.frameLock)
        {
            if (!force && (this.isStopped ||
                this.isDisposed ||
                this.tokenSource.IsCancellationRequested))
            {
                return Task.CompletedTask;
            }

            // Snapshot: everything built so far plus the frame that will carry accumulated data.
            // Data appended after this point does not extend this flush.
            var targetFrameCount = this.framesBuilt + (this.hasAccumulatedData ? 1 : 0);

            this.TryStartFlushLocked(token);

            if (this.isDisposed || this.framesCompleted >= targetFrameCount)
            {
                return Task.CompletedTask;
            }

            var waiterNode = this.flushWaiters.AddLast(new FlushWaiter(targetFrameCount));
            return this.WaitForFlushAsync(waiterNode, token);
        }
    }

    private void TryFlush(CancellationToken token)
    {
        lock (this.frameLock)
        {
            this.TryStartFlushLocked(token);
            this.CompleteFlushWaitersLocked();
        }
    }

    private bool HasDataToSendLocked()
        => this.hasAccumulatedData || this.pendingFrames.Count > 0;

    private Task? TryStartFlushLocked(CancellationToken token)
    {
        if (this.isDisposed || !this.HasDataToSendLocked())
        {
            return null;
        }

        if (this.isBusy)
        {
            return null;
        }

        this.isBusy = true;

        AgentToServer message;
        if (this.pendingFrames.Count > 0)
        {
            var pending = this.pendingFrames.Dequeue();
            message = pending.Message;
            this.pendingMessageBytes -= pending.Bytes;
        }
        else
        {
            message = this.currentFrame.Build();
            this.framesBuilt++;
            this.hasAccumulatedData = false;
        }

        // Always dispatch asynchronously so a synchronously completing transport cannot
        // recursively drain the queue and eventually overflow the call stack. The send task
        // must start even when token is canceled so SendMessageAsync can release the busy state.
        this.flushTask = Task.Run(
            () => this.SendMessageAsync(message, token),
            CancellationToken.None);

        return this.flushTask;
    }

    private void CompleteFlushWaitersLocked()
    {
        while (this.flushWaiters.First is { } first &&
            (this.isDisposed || first.Value.Target <= this.framesCompleted))
        {
            this.flushWaiters.RemoveFirst();
            first.Value.Completion.TrySetResult(true);
        }
    }

    private async Task SendMessageAsync(AgentToServer message, CancellationToken token)
    {
        try
        {
            OpAmpClientEventSource.Log.SendingMessage();

            await this.transport.SendAsync(message, token, this.applyAssignedInstanceUid)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OpAmpClientEventSource.Log.SendMessageException(ex);
        }
        finally
        {
            // HTTP sends include synchronous response dispatch. Keep the slot busy until
            // every listener has returned so later responses cannot overtake this one.
            this.ReleaseBusy();
            this.TryFlush(token);
        }
    }

    // The transport calls this right before it serializes the frame, so frames queued or
    // waiting to be sent when the server assigned a new UID are still sent with it.
    private void ApplyAssignedInstanceUid(AgentToServer message)
    {
        lock (this.frameLock)
        {
            if (this.assignedInstanceUid is { } instanceUid)
            {
                message.InstanceUid = instanceUid;
            }
        }
    }

    private void OnServerFrameReceived(ServerToAgent message)
    {
        // Handled here rather than by an AgentIdentificationMessage listener so the
        // new UID is applied before any field listener can queue another frame.
        if (message.AgentIdentification is { } agentIdentification)
        {
            this.SetInstanceUid(agentIdentification.NewInstanceUid);
        }
    }

    private void SetInstanceUid(ByteString instanceUid)
    {
        if (instanceUid.Length != 16)
        {
            OpAmpClientEventSource.Log.InvalidInstanceUid($"expected 16 bytes, received {instanceUid.Length}");
            return;
        }

        if (instanceUid.Span.SequenceEqual(EmptyInstanceUid))
        {
            OpAmpClientEventSource.Log.InvalidInstanceUid("all bytes are zero");
            return;
        }

        lock (this.frameLock)
        {
            this.assignedInstanceUid = instanceUid;
        }
    }

    private void ReleaseBusy()
    {
        lock (this.frameLock)
        {
            this.isBusy = false;
            this.framesCompleted++;
        }
    }

    // A queued frame and the bytes it counts towards MaxPendingMessageBytes.
    private readonly struct PendingFrame(AgentToServer message, long bytes)
    {
        public AgentToServer Message { get; } = message;

        public long Bytes { get; } = bytes;
    }

    private readonly struct FlushWaiter
    {
        public FlushWaiter(long target)
        {
            this.Target = target;
        }

        public long Target { get; }

        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ServerFrameHandler : IOpAmpListener<ServerToAgentMessage>
    {
        public ServerFrameHandler(Action<ServerToAgent> callback)
        {
            this.Callback = callback;
        }

        public Action<ServerToAgent> Callback { get; }

        public void HandleMessage(ServerToAgentMessage message) => this.Callback(message.Message);
    }
}
