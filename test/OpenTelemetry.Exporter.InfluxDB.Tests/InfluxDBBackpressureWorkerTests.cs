// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Xunit;

namespace OpenTelemetry.Exporter.InfluxDB.Tests;

public class InfluxDBBackpressureWorkerTests
{
    [Fact]
    public async Task WaitModeBlocksUntilCapacityBecomesAvailable()
    {
        using var gate = new ManualResetEventSlim(false);
        using var writer = new TestPayloadWriter(gate);
        using var worker = new InfluxDBBackpressureWorker(1, BackpressureMode.Wait, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out var droppedWriteCount));
        Assert.Equal(0, droppedWriteCount);

        await writer.WaitForFirstWriteAsync();

        var enqueueTask = Task.Run(() =>
        {
            var enqueued = worker.Enqueue(["second"], out var droppedWrites);
            return (enqueued, droppedWrites);
        });

        await Task.Delay(200);
        Assert.False(enqueueTask.IsCompleted);

        gate.Set();

        var enqueueResult = await enqueueTask;
        Assert.True(enqueueResult.enqueued);
        Assert.Equal(0, enqueueResult.droppedWrites);
        Assert.True(worker.Flush(5000));

        var writes = writer.Writes.ToArray();
        Assert.Collection(
            writes,
            payload => Assert.Equal(["first"], payload),
            payload => Assert.Equal(["second"], payload));
    }

    [Fact]
    public async Task DropNewestModeDropsCurrentPayloadWhenCapacityIsReached()
    {
        using var gate = new ManualResetEventSlim(false);
        using var writer = new TestPayloadWriter(gate);
        using var worker = new InfluxDBBackpressureWorker(2, BackpressureMode.DropNewest, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out _));
        await writer.WaitForFirstWriteAsync();

        Assert.True(worker.Enqueue(["second"], out var secondDroppedWriteCount));
        Assert.Equal(0, secondDroppedWriteCount);

        var enqueued = worker.Enqueue(["third"], out var droppedWriteCount);

        Assert.False(enqueued);
        Assert.Equal(1, droppedWriteCount);

        gate.Set();
        Assert.True(worker.Flush(5000));

        var writes = writer.Writes.ToArray();
        Assert.Collection(
            writes,
            payload => Assert.Equal(["first"], payload),
            payload => Assert.Equal(["second"], payload));
    }

    [Fact]
    public async Task DropOldestModeDropsQueuedPayloadWhenCapacityIsReached()
    {
        using var gate = new ManualResetEventSlim(false);
        using var writer = new TestPayloadWriter(gate);
        using var worker = new InfluxDBBackpressureWorker(2, BackpressureMode.DropOldest, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out _));
        await writer.WaitForFirstWriteAsync();

        Assert.True(worker.Enqueue(["second"], out var secondDroppedWriteCount));
        Assert.Equal(0, secondDroppedWriteCount);

        var enqueued = worker.Enqueue(["third"], out var droppedWriteCount);

        Assert.True(enqueued);
        Assert.Equal(1, droppedWriteCount);

        gate.Set();
        Assert.True(worker.Flush(5000));

        var writes = writer.Writes.ToArray();
        Assert.Collection(
            writes,
            payload => Assert.Equal(["first"], payload),
            payload => Assert.Equal(["third"], payload));
    }

    [Fact]
    public async Task DropOldestModeDoesNotLeakSignalsAcrossRepeatedDrops()
    {
        using var gate = new ManualResetEventSlim(false);
        using var writer = new TestPayloadWriter(gate);
        using var worker = new InfluxDBBackpressureWorker(2, BackpressureMode.DropOldest, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out _));
        await writer.WaitForFirstWriteAsync();

        Assert.True(worker.Enqueue(["second"], out _));

        for (var i = 0; i < 50; i++)
        {
            Assert.True(worker.Enqueue([$"payload-{i}"], out var droppedWriteCount));
            Assert.Equal(1, droppedWriteCount);
        }

        gate.Set();

        Assert.True(worker.Flush(5000));

        var writes = writer.Writes.ToArray();
        Assert.Equal(["first"], writes[0]);
        Assert.Equal(["payload-49"], writes[writes.Length - 1]);
        Assert.Equal(2, writes.Length);
    }

    [Fact]
    public async Task FlushReturnsFalseWhenBackgroundWriteFails()
    {
        using var writer = new ThrowingPayloadWriter();
        using var worker = new InfluxDBBackpressureWorker(1, BackpressureMode.Wait, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out _));
        await writer.WaitForWriteAttemptAsync();

        Assert.False(worker.Flush(5000));
        Assert.True(worker.Flush(5000));
    }

    [Fact]
    public async Task FlushReturnsFalseWhenTimeoutExpires()
    {
        using var gate = new ManualResetEventSlim(false);
        using var writer = new TestPayloadWriter(gate);
        using var worker = new InfluxDBBackpressureWorker(1, BackpressureMode.Wait, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out _));
        await writer.WaitForFirstWriteAsync();

        Assert.False(worker.Flush(10));

        gate.Set();
        Assert.True(worker.Flush(5000));
    }

    [Fact]
    public async Task FlushWaitsForBlockedEnqueueToComplete()
    {
        using var firstWriteGate = new ManualResetEventSlim(false);
        using var secondWriteGate = new ManualResetEventSlim(false);
        using var writer = new SequencedPayloadWriter(firstWriteGate, secondWriteGate);
        using var worker = new InfluxDBBackpressureWorker(1, BackpressureMode.Wait, writer, _ => { });

        Assert.True(worker.Enqueue(["first"], out _));
        await writer.WaitForFirstWriteAsync();

        var enqueueTask = Task.Run(() => worker.Enqueue(["second"], out _));
        await Task.Delay(100);
        Assert.False(enqueueTask.IsCompleted);

        var flushTask = Task.Run(() => worker.Flush(5000));
        await Task.Delay(100);
        Assert.False(flushTask.IsCompleted);

        firstWriteGate.Set();
        await writer.WaitForSecondWriteAsync();

        await Task.Delay(100);
        Assert.False(flushTask.IsCompleted);

        secondWriteGate.Set();

        Assert.True(await enqueueTask);
        Assert.True(await flushTask);
    }

    private sealed class TestPayloadWriter : IInfluxDBExportPayloadWriter, IDisposable
    {
        private readonly ManualResetEventSlim gate;
        private readonly TaskCompletionSource<bool> firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestPayloadWriter(ManualResetEventSlim gate)
        {
            this.gate = gate;
        }

        public ConcurrentQueue<string[]> Writes { get; } = new();

        public void Dispose()
        {
        }

        public Task WaitForFirstWriteAsync() => this.firstWriteStarted.Task;

        public Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken)
        {
            this.Writes.Enqueue(lineProtocol.ToArray());
            this.firstWriteStarted.TrySetResult(true);
            this.gate.Wait(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPayloadWriter : IInfluxDBExportPayloadWriter, IDisposable
    {
        private readonly TaskCompletionSource<bool> writeAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose()
        {
        }

        public Task WaitForWriteAttemptAsync() => this.writeAttempted.Task;

        public Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken)
        {
            this.writeAttempted.TrySetResult(true);
            throw new InvalidOperationException("Simulated export failure.");
        }
    }

    private sealed class SequencedPayloadWriter : IInfluxDBExportPayloadWriter, IDisposable
    {
        private readonly ManualResetEventSlim firstWriteGate;
        private readonly ManualResetEventSlim secondWriteGate;
        private readonly TaskCompletionSource<bool> firstWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> secondWriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int writeCount;

        public SequencedPayloadWriter(ManualResetEventSlim firstWriteGate, ManualResetEventSlim secondWriteGate)
        {
            this.firstWriteGate = firstWriteGate;
            this.secondWriteGate = secondWriteGate;
        }

        public void Dispose()
        {
        }

        public Task WaitForFirstWriteAsync() => this.firstWriteStarted.Task;

        public Task WaitForSecondWriteAsync() => this.secondWriteStarted.Task;

        public Task WriteAsync(IReadOnlyCollection<string> lineProtocol, CancellationToken cancellationToken)
        {
            var currentWrite = Interlocked.Increment(ref this.writeCount);
            if (currentWrite == 1)
            {
                this.firstWriteStarted.TrySetResult(true);
                this.firstWriteGate.Wait(cancellationToken);
            }
            else
            {
                this.secondWriteStarted.TrySetResult(true);
                this.secondWriteGate.Wait(cancellationToken);
            }

            return Task.CompletedTask;
        }
    }
}
