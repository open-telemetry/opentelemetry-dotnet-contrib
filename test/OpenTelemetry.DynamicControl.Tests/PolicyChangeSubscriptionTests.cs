// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.DynamicControl.Internal.Diagnostics;
using OpenTelemetry.DynamicControl.Internal.Store;
using OpenTelemetry.Tests;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyChangeSubscriptionTests
{
    [Fact]
    public async Task Enqueue_SingleSnapshot_Delivered()
    {
        var notifier = new PolicyChangeNotifier();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();
        var subscription = notifier.Add(s => delivered.Enqueue(s));

        subscription.Enqueue(SnapshotAtRevision(1));

        await WaitHelper.WaitUntil(() => delivered.Count == 1);
        Assert.Equal(1, delivered.Single().Revision);
    }

    [Fact]
    public async Task Enqueue_WhileDispatching_CoalescesToLatest()
    {
        var notifier = new PolicyChangeNotifier();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();
        using var callbackStarted = new SemaphoreSlim(0);
        using var releaseCallback = new SemaphoreSlim(0);
        var firstCallback = true;

        var subscription = notifier.Add(s =>
        {
            if (firstCallback)
            {
                firstCallback = false;
                callbackStarted.Release();
                releaseCallback.Wait(TimeSpan.FromSeconds(10));
            }

            delivered.Enqueue(s);
        });

        subscription.Enqueue(SnapshotAtRevision(1));
        await callbackStarted.WaitAsync(TimeSpan.FromSeconds(10));

        // Enqueue several more snapshots while the first callback is still blocked.
        // Only the newest should ever be delivered next.
        subscription.Enqueue(SnapshotAtRevision(2));
        subscription.Enqueue(SnapshotAtRevision(3));
        subscription.Enqueue(SnapshotAtRevision(4));

        releaseCallback.Release();

        await WaitHelper.WaitUntil(() => delivered.Count == 2);

        var results = delivered.ToArray();
        Assert.Equal(1, results[0].Revision);
        Assert.Equal(4, results[1].Revision);
    }

    [Fact]
    public async Task Enqueue_NeverOverlapsCallbackInvocations()
    {
        var notifier = new PolicyChangeNotifier();
        var overlapDetected = false;
        var concurrentCount = 0;

        var subscription = notifier.Add(_ =>
        {
            if (Interlocked.Increment(ref concurrentCount) > 1)
            {
                overlapDetected = true;
            }

            Thread.Sleep(1);
            Interlocked.Decrement(ref concurrentCount);
        });

        for (var i = 1; i <= 50; i++)
        {
            subscription.Enqueue(SnapshotAtRevision(i));
        }

        await WaitHelper.WaitUntil(() => Volatile.Read(ref concurrentCount) == 0, 5000);
        Assert.False(overlapDetected, "Callback invocations for one subscription must never overlap");
    }

    [Fact]
    public async Task Enqueue_CallbackThrows_ExceptionIsolated_SubsequentDeliverySucceeds()
    {
        using var listener = new InMemoryEventListener(DynamicControlEventSource.Log, EventLevel.Warning);
        var notifier = new PolicyChangeNotifier();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();

        var subscription = notifier.Add(s =>
        {
            if (s.Revision == 1)
            {
                throw new InvalidOperationException("boom");
            }

            delivered.Enqueue(s);
        });

        subscription.Enqueue(SnapshotAtRevision(1));
        await WaitHelper.WaitUntil(() => listener.Events.Any(e => e.EventId == 1));

        subscription.Enqueue(SnapshotAtRevision(2));
        await WaitHelper.WaitUntil(() => delivered.Count == 1);

        Assert.Equal(2, delivered.Single().Revision);
        Assert.Contains(listener.Events, e => e.EventId == 1 && e.Payload!.Single() is string message && message.Contains("boom"));
    }

    [Fact]
    public async Task Dispose_StopsFutureDelivery()
    {
        var notifier = new PolicyChangeNotifier();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();
        var subscription = notifier.Add(s => delivered.Enqueue(s));

        subscription.Enqueue(SnapshotAtRevision(1));
        await WaitHelper.WaitUntil(() => delivered.Count == 1);

        subscription.Dispose();
        subscription.Enqueue(SnapshotAtRevision(2));

        await Task.Delay(50);
        Assert.Single(delivered);
    }

    [Fact]
    public void Dispose_DoesNotWaitForInFlightCallback()
    {
        var notifier = new PolicyChangeNotifier();
        using var callbackStarted = new SemaphoreSlim(0);
        using var releaseCallback = new SemaphoreSlim(0);

        var subscription = notifier.Add(_ =>
        {
            callbackStarted.Release();
            releaseCallback.Wait(TimeSpan.FromSeconds(10));
        });

        subscription.Enqueue(SnapshotAtRevision(1));
        Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(10)));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        subscription.Dispose();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, "Dispose must not block on an in-flight callback");
        releaseCallback.Release();
    }

    [Fact]
    public async Task Dispose_WhileCallbackInProgress_DropsPendingDelivery()
    {
        var notifier = new PolicyChangeNotifier();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<long>();
        using var callbackStarted = new SemaphoreSlim(0);
        using var releaseCallback = new SemaphoreSlim(0);

        var subscription = notifier.Add(snapshot =>
        {
            callbackStarted.Release();
            releaseCallback.Wait(TimeSpan.FromSeconds(10));
            delivered.Enqueue(snapshot.Revision);
        });

        subscription.Enqueue(SnapshotAtRevision(1));
        await callbackStarted.WaitAsync(TimeSpan.FromSeconds(10));

        subscription.Enqueue(SnapshotAtRevision(2));
        subscription.Dispose();
        releaseCallback.Release();

        await WaitHelper.WaitUntil(() => delivered.Count == 1);
        await Task.Delay(50);

        Assert.Equal([1], delivered);
        Assert.True(notifier.Subscribers.IsEmpty);
    }

    [Fact]
    public void Dispose_MultipleTimes_NoOp()
    {
        var notifier = new PolicyChangeNotifier();
        var subscription = notifier.Add(_ => { });

        subscription.Dispose();
        subscription.Dispose();

        Assert.True(notifier.Subscribers.IsEmpty);
    }

    private static PolicyStoreSnapshot SnapshotAtRevision(long revision)
        => new(revision, []);
}
