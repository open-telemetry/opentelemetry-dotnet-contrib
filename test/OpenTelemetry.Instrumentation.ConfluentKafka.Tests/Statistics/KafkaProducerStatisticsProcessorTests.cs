// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Instrumentation.ConfluentKafka.Tests;

[Collection(KafkaProducerStatisticsTestGroup.Name)]
public class KafkaProducerStatisticsProcessorTests
{
    [Fact]
    public async Task UserCallbackDoesNotWaitForStatisticsProcessing()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var processor = new KafkaProducerStatisticsProcessor(_ =>
        {
            started.TrySetResult(true);
            release.Wait(TestContext.Current.CancellationToken);
        });

        try
        {
            Assert.True(processor.TryEnqueue("first"));
            await WaitForAsync(started.Task);
            var called = false;
            var handler = KafkaProducerStatisticsHandler.Create<string, string>(
                json => processor.TryEnqueue(json),
                (_, _) => called = true);

            var callback = Task.Run(() => handler(null!, "second"), TestContext.Current.CancellationToken);
            await WaitForAsync(callback);
            Assert.True(called);
            Assert.False(processor.Completion.IsCompleted);
        }
        finally
        {
            release.Set();
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }
    }

    [Fact]
    public async Task AcceptedPayloadsAreProcessedInOrder()
    {
        var received = new ConcurrentQueue<string>();
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var processor = new KafkaProducerStatisticsProcessor(json =>
        {
            received.Enqueue(json);
            if (received.Count == 3)
            {
                processed.TrySetResult(true);
            }
        });

        try
        {
            Assert.True(processor.TryEnqueue("first"));
            Assert.True(processor.TryEnqueue("second"));
            Assert.True(processor.TryEnqueue("third"));
            await WaitForAsync(processed.Task);
            Assert.Equal(["first", "second", "third"], received);
        }
        finally
        {
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }
    }

    [Fact]
    public async Task FullQueueRejectsNewPayloadAndReportsWarning()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new ConcurrentQueue<string>();
        Action<string> update = json =>
        {
            if (json == "first")
            {
                started.TrySetResult(true);
                release.Wait(TestContext.Current.CancellationToken);
            }

            received.Enqueue(json);
            if (received.Count == 2)
            {
                processed.TrySetResult(true);
            }
        };
        using var processor = new KafkaProducerStatisticsProcessor(
            update, capacity: 1);

        try
        {
            Assert.True(processor.TryEnqueue("first"));
            await WaitForAsync(started.Task);
            Assert.True(processor.TryEnqueue("second"));
            using var listener = new StatisticsEventListener();
            var called = false;
            var accepted = true;
            var handler = KafkaProducerStatisticsHandler.Create<string, string>(
                json => accepted = processor.TryEnqueue(json),
                (_, _) => called = true);

            handler(null!, "dropped");
            Assert.False(accepted);
            Assert.True(called);
            Assert.Contains(3, listener.EventIds);
            release.Set();
            await WaitForAsync(processed.Task);
            Assert.Equal(["first", "second"], received);
        }
        finally
        {
            release.Set();
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }
    }

    [Fact]
    public async Task ProcessingFailureDoesNotStopReader()
    {
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var processor = new KafkaProducerStatisticsProcessor(json =>
        {
            if (json == "invalid")
            {
                throw new InvalidOperationException("statistics processing failure");
            }

            processed.TrySetResult(true);
        });

        try
        {
            Assert.True(processor.TryEnqueue("invalid"));
            Assert.True(processor.TryEnqueue("valid"));
            await WaitForAsync(processed.Task);
        }
        finally
        {
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }
    }

    [Fact]
    public async Task DisposalDiscardsQueuedPayloadsWithoutWaitingForInFlightProcessing()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new ConcurrentQueue<string>();
        using var processor = new KafkaProducerStatisticsProcessor(json =>
        {
            started.TrySetResult(true);
            release.Wait(TestContext.Current.CancellationToken);
            received.Enqueue(json);
        });

        try
        {
            Assert.True(processor.TryEnqueue("in-flight"));
            await WaitForAsync(started.Task);
            Assert.True(processor.TryEnqueue("queued"));
            processor.Dispose();
            Assert.False(processor.TryEnqueue("late"));
            Assert.False(processor.Completion.IsCompleted);
        }
        finally
        {
            release.Set();
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }

        Assert.Equal(["in-flight"], received);
    }

    [Fact]
    public async Task IdleReaderStopsOnDisposal()
    {
        using var processor = new KafkaProducerStatisticsProcessor(_ => Assert.Fail("No payload should be processed."));
        processor.Dispose();
        processor.Dispose();
        await WaitForAsync(processor.Completion);
        Assert.False(processor.TryEnqueue("late"));
    }

    [Fact]
    public async Task SlowProducerDoesNotBlockAnotherProducer()
    {
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var first = new KafkaProducerStatisticsProcessor(_ =>
        {
            started.TrySetResult(true);
            release.Wait(TestContext.Current.CancellationToken);
        });
        using var second = new KafkaProducerStatisticsProcessor(_ => processed.TrySetResult(true));

        try
        {
            Assert.True(first.TryEnqueue("slow"));
            await WaitForAsync(started.Task);
            Assert.True(second.TryEnqueue("independent"));
            await WaitForAsync(processed.Task);
            Assert.False(first.Completion.IsCompleted);
        }
        finally
        {
            release.Set();
            first.Dispose();
            second.Dispose();
            await WaitForAsync(Task.WhenAll(first.Completion, second.Completion));
        }
    }

    [Fact]
    public async Task MalformedQueuedStatisticsDoNotStopSubsequentUpdates()
    {
        using var registration = new KafkaProducerMetricsRegistration();
        try
        {
            registration.Enqueue("{");
            registration.Enqueue(KafkaProducerStatisticsTests.ReadFixture("active"));
            Assert.True(SpinWait.SpinUntil(() => registration.Snapshot?.Records == 10, TimeSpan.FromSeconds(10)));
        }
        finally
        {
            registration.Dispose();
            await WaitForAsync(registration.Completion);
        }
    }

    [Fact]
    public async Task ConcurrentWritersPreserveEveryAcceptedPayload()
    {
        const int PayloadCount = 32;
        var received = new ConcurrentQueue<string>();
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<string> update = json =>
        {
            received.Enqueue(json);
            if (received.Count == PayloadCount)
            {
                processed.TrySetResult(true);
            }
        };
        using var processor = new KafkaProducerStatisticsProcessor(update, capacity: PayloadCount);
        try
        {
            var writers = Enumerable.Range(0, PayloadCount).Select(index => Task.Run(
                () => processor.TryEnqueue($"payload-{index}"),
                TestContext.Current.CancellationToken));
            var results = await Task.WhenAll(writers);
            Assert.All(results, accepted => Assert.True(accepted));
            await WaitForAsync(processed.Task);
            Assert.Equal(PayloadCount, received.Distinct().Count());
        }
        finally
        {
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }
    }

    [Fact]
    public async Task MalformedPayloadRetainsLastValidSnapshotAndInvokesUserCallback()
    {
        using var registration = new KafkaProducerMetricsRegistration();
        registration.Update(KafkaProducerStatisticsTests.ReadFixture("active"));
        var snapshot = registration.Snapshot;
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var processor = new KafkaProducerStatisticsProcessor(json =>
        {
            try
            {
                registration.Update(json);
            }
            finally
            {
                processed.TrySetResult(true);
            }
        });
        try
        {
            var called = false;
            var handler = KafkaProducerStatisticsHandler.Create<string, string>(
                json => processor.TryEnqueue(json),
                (_, _) => called = true);
            handler(null!, "{");
            Assert.True(called);
            await WaitForAsync(processed.Task);
            Assert.Same(snapshot, registration.Snapshot);
        }
        finally
        {
            processor.Dispose();
            await WaitForAsync(processor.Completion);
        }
    }

    [Fact]
    public async Task QueuedUpdatesCannotResurrectDisposedRegistration()
    {
        using var registration = new KafkaProducerMetricsRegistration();
        registration.Enqueue(KafkaProducerStatisticsTests.ReadFixture("active"));
        registration.Dispose();
        registration.Enqueue(KafkaProducerStatisticsTests.ReadFixture("active"));
        await WaitForAsync(registration.Completion);
        Assert.Null(registration.Snapshot);
    }

    internal static async Task WaitForAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(task, completed);
        await task;
    }

    private sealed class StatisticsEventListener : EventListener
    {
        private readonly int creatingThreadId = Environment.CurrentManagedThreadId;

        public ConcurrentQueue<int> EventIds { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "OpenTelemetry-Instrumentation-ConfluentKafka")
            {
                this.EnableEvents(eventSource, EventLevel.Warning, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (Environment.CurrentManagedThreadId == this.creatingThreadId)
            {
                this.EventIds.Enqueue(eventData.EventId);
            }
        }
    }
}
