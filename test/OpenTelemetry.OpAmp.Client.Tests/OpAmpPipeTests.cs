// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.Tracing;
using Google.Protobuf;
using OpAmp.Proto.V1;
using OpenTelemetry.OpAmp.Client.Internal;
using OpenTelemetry.OpAmp.Client.Internal.Services.Heartbeat;
using OpenTelemetry.OpAmp.Client.Settings;
using OpenTelemetry.OpAmp.Client.Tests.Mocks;
using OpenTelemetry.OpAmp.Client.Tests.Tools;
using OpenTelemetry.Tests;

namespace OpenTelemetry.OpAmp.Client.Tests;

public abstract class OpAmpPipeTests
{
    [Fact]
    public async Task OpAmpPipe_IsThreadSafe_WhenAppendingConcurrently()
    {
        var taskCount = 20;

        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

#if NET
        await Parallel.ForEachAsync(Enumerable.Range(0, taskCount), (_, _) =>
        {
            AppendIdentification(pipe);
            return ValueTask.CompletedTask;
        });
#else
        Parallel.ForEach(Enumerable.Range(0, taskCount), (_, _) =>
        {
            AppendIdentification(pipe);
        });
#endif

        Assert.Single(transport.Messages);

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await transport.WaitForMessagesAsync(2);
        transport.CompleteNextSend();

        var messages = transport.Messages;
        var sequenceNumbers = messages
            .Select(m => m.SequenceNum)
            .ToArray();

        Assert.Equal([1UL, 2UL], sequenceNumbers);
        Assert.All(messages, message => Assert.NotNull(message.AgentDescription));
    }

    [Fact]
    public async Task OpAmpPipe_PreservesAccumulatedCustomMessages()
    {
        const int customMessageCount = 3;

        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        for (var i = 0; i < customMessageCount; i++)
        {
            AppendCustomMessage(pipe, i);
        }

        Assert.Single(transport.Messages);
        using var flushCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var flushTask = pipe.FlushAsync(flushCancellation.Token);
        Assert.False(flushTask.IsCompleted);

        for (var expectedMessageCount = 2;
            expectedMessageCount <= customMessageCount + 1;
            expectedMessageCount++)
        {
            transport.CompleteNextSend();
            processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));
            await transport.WaitForMessagesAsync(expectedMessageCount);
        }

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));
        await flushTask;

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL, 3UL, 4UL], [.. messages.Select(m => m.SequenceNum)]);

        for (var i = 0; i < customMessageCount; i++)
        {
            var customMessage = messages[i + 1].CustomMessage;
            Assert.NotNull(customMessage);
            Assert.Equal($"capability-{i}", customMessage.Capability);
            Assert.Equal($"type-{i}", customMessage.Type);
            Assert.Equal([(byte)i], customMessage.Data.ToByteArray());
        }
    }

    [Fact]
    public async Task OpAmpPipe_RejectsCustomMessagesBeyondQueueLimit()
    {
        const int maxPendingCustomMessages = 2;

        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings
        {
            MaxPendingCustomMessages = maxPendingCustomMessages,
        };
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        for (var i = 0; i < maxPendingCustomMessages; i++)
        {
            AppendCustomMessage(pipe, i);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => AppendCustomMessage(pipe, maxPendingCustomMessages));

        Assert.Contains(maxPendingCustomMessages.ToString(), exception.Message);
        Assert.Single(transport.Messages);

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));
        await transport.WaitForMessagesAsync(2);

        // Dequeueing a pending frame must make capacity available immediately.
        AppendCustomMessage(pipe, maxPendingCustomMessages);
    }

    [Fact]
    public async Task OpAmpPipe_RejectsCustomMessagesBeyondQueuePayloadByteLimit()
    {
        const int payloadSize = 4;
        const int maxPendingCustomMessageBytes = 8;
        const int messagesAtLimit = maxPendingCustomMessageBytes / payloadSize;

        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings
        {
            MaxPendingCustomMessageBytes = maxPendingCustomMessageBytes,
        };
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();
        var payload = new byte[payloadSize];

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        for (var i = 0; i < messagesAtLimit; i++)
        {
            AppendCustomMessage(pipe, i, payload);
        }

        var fullQueueException = Assert.Throws<InvalidOperationException>(
            () => AppendCustomMessage(pipe, messagesAtLimit, new byte[1]));
        Assert.Contains(maxPendingCustomMessageBytes.ToString(), fullQueueException.Message);
        Assert.Single(transport.Messages);

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));
        await transport.WaitForMessagesAsync(2);

        var firstCustomMessage = transport.Messages[1];
        Assert.Equal(2UL, firstCustomMessage.SequenceNum);
        Assert.Equal("type-0", firstCustomMessage.CustomMessage.Type);

        // Dequeueing a pending frame must make its payload-byte capacity available immediately.
        AppendCustomMessage(pipe, messagesAtLimit + 1, payload);
        Assert.Throws<InvalidOperationException>(
            () => AppendCustomMessage(pipe, messagesAtLimit + 2, new byte[1]));
    }

    [Fact]
    public async Task OpAmpPipe_FlushAsyncCompletes_WhenNoMessagesArePending()
    {
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        await pipe.FlushAsync();

        Assert.Empty(transport.Messages);
    }

    [Fact]
    public async Task OpAmpPipe_FlushAsyncIsThreadSafe_WhenCalledConcurrentlyWithoutPendingMessages()
    {
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

#if NET
        await Parallel.ForEachAsync(Enumerable.Range(0, 20), async (_, token) =>
        {
            await pipe.FlushAsync(token);
        });
#else
        Parallel.ForEach(Enumerable.Range(0, 20), async (_) =>
        {
            await pipe.FlushAsync();
        });
#endif

        Assert.Empty(transport.Messages);
    }

    [Fact]
    public async Task OpAmpPipe_FlushesAccumulatedData_WhenCurrentSendFails()
    {
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        AppendHeartbeat(pipe);
        transport.FaultNextSend(new InvalidOperationException("send failed"));

        await transport.WaitForMessagesAsync(2);
        transport.CompleteNextSend();

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL], [.. messages.Select(m => m.SequenceNum)]);
        Assert.NotNull(messages[0].AgentDescription);
        Assert.NotNull(messages[1].Health);
    }

    [Fact]
    public async Task OpAmpPipe_LogsSendFailure_WhenCurrentSendFails()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var failureMessage = $"send failed {Guid.NewGuid():N}";

        AppendHeartbeat(pipe);
        await transport.WaitForMessagesAsync(1);
        transport.FaultNextSend(new InvalidOperationException(failureMessage));

        await eventListener.WaitForEventAsync(
            e => e.EventName == nameof(OpAmpClientEventSource.FailedToSendMessage)
                && e.Payload![0] is string exception
                && exception.Contains(failureMessage),
            $"{nameof(OpAmpClientEventSource.FailedToSendMessage)} containing '{failureMessage}'",
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task OpAmpPipe_LogsQueueAction_WhenMessageIsAppended()
    {
        using var eventListener = new InMemoryEventListener(OpAmpClientEventSource.Log, EventLevel.Verbose);
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendHeartbeat(pipe);

        await eventListener.WaitForEventAsync(
            e => e.EventName == nameof(OpAmpClientEventSource.QueueingHeartbeatMessage),
            nameof(OpAmpClientEventSource.QueueingHeartbeatMessage),
            TimeSpan.FromSeconds(5));

        await transport.WaitForMessagesAsync(1);
        transport.CompleteNextSend();
    }

    [Fact]
    public async Task OpAmpPipe_StopAsyncWaitsForAccumulatedDataBeforeSendingDisconnect()
    {
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        AppendHeartbeat(pipe);
        var stopTask = pipe.StopAsync();

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await transport.WaitForMessagesAsync(2);
        await Assert.ThrowsAsync<TimeoutException>(
            () => transport.WaitForMessagesAsync(3, TimeSpan.FromMilliseconds(250)));

        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await transport.WaitForMessagesAsync(3);
        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        await stopTask;

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL, 3UL], [.. messages.Select(m => m.SequenceNum)]);
        Assert.NotNull(messages[0].AgentDescription);
        Assert.NotNull(messages[1].Health);
        Assert.NotNull(messages[2].AgentDisconnect);
    }

    [Fact]
    public async Task OpAmpPipe_StopAsyncCanBeCanceledWhileDrainingQueuedData()
    {
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);

        AppendHeartbeat(pipe);
        using var cts = new CancellationTokenSource();
        var stopTask = pipe.StopAsync(cts.Token);

        Assert.False(stopTask.IsCompleted);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopTask);

        Assert.Single(transport.Messages);
    }

    [Fact]
    public async Task OpAmpPipe_StopAsyncCanBeCanceledWhileWaitingForDisconnectResponse()
    {
        using var transport = this.GetTransport();
        var settings = new OpAmpClientSettings();
        var processor = new FrameProcessor();
        using var pipe = new OpAmpPipe(settings, processor, transport);
        var serverFrame = new ServerToAgent().ToByteArray();

        AppendIdentification(pipe);
        await transport.WaitForMessagesAsync(1);
        transport.CompleteNextSend();
        processor.OnServerFrame(new ReadOnlySequence<byte>(serverFrame));

        using var cts = new CancellationTokenSource();
        var stopTask = pipe.StopAsync(cts.Token);

        await transport.WaitForMessagesAsync(2);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopTask);

        var messages = transport.Messages.ToArray();
        Assert.Equal([1UL, 2UL], [.. messages.Select(m => m.SequenceNum)]);
        Assert.NotNull(messages[1].AgentDisconnect);
    }

    internal static void AppendIdentification(OpAmpPipe pipe)
    => pipe.AppendMessage(MessageBuilderHelper.AppendIdentification);

    internal static void AppendHeartbeat(OpAmpPipe pipe)
        => pipe.AppendMessage(MessageBuilderHelper.AppendHeartbeat(CreateHealthReport()));

    internal static void AppendCustomMessage(OpAmpPipe pipe, int index)
        => pipe.AppendCustomMessage(
            $"capability-{index}",
            $"type-{index}",
            new byte[] { (byte)index });

    internal static void AppendCustomMessage(OpAmpPipe pipe, int index, ReadOnlyMemory<byte> payload)
        => pipe.AppendCustomMessage(
            $"capability-{index}",
            $"type-{index}",
            payload);

    internal abstract MockControlledTransport GetTransport(Action? firstSendCallback = null);

    private static HealthReport CreateHealthReport() => new()
    {
        StartTime = 1,
        StatusTime = 2,
        IsHealthy = true,
        Status = "OK",
    };
}
