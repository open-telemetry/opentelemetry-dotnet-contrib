// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using OpenTelemetry.DynamicControl.Internal.Sources;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

/// <summary>
/// Bounded, multi-thread tests for the store-subscription guarantees, following the
/// repo's existing thread-safety test precedent.
/// </summary>
public class PolicyStoreConcurrencyTests
{
    [Fact]
    public async Task Subscribe_ConcurrentWithCommit_DoesNotMissCommittedSnapshot()
    {
        const int attemptCount = 100;

        for (var attempt = 0; attempt < attemptCount; attempt++)
        {
            var store = new PolicyStore();
            var delivered = new ConcurrentQueue<long>();
            var sourceId = $"source-{attempt}";
            using var start = new ManualResetEventSlim();
            IDisposable? subscription = null;

            var subscribeTask = Task.Run(() =>
            {
                start.Wait();
                subscription = store.Subscribe(snapshot => delivered.Enqueue(snapshot.Revision));
            });

            var commitTask = Task.Run(() =>
            {
                start.Wait();
                store.ReplaceSource(CreateSnapshot(sourceId, sequence: 1));
            });

            start.Set();
            await Task.WhenAll(subscribeTask, commitTask);

            Assert.NotNull(subscription);
            using (subscription)
            {
                await WaitUntil(() => !delivered.IsEmpty && delivered.Last() == store.Current.Revision);
                Assert.Equal(1, store.Current.Revision);
            }
        }
    }

    [Fact]
    public async Task Subscribe_ManyConcurrentCommits_DeliveredRevisionsNeverDecrease_ConvergeToFinal()
    {
        const int producerCount = 6;
        const int commitsPerProducer = 500;
        var store = new PolicyStore();

        var deliveredA = new ConcurrentQueue<long>();
        var deliveredB = new ConcurrentQueue<long>();
        var reentrancyA = 0;
        var reentrancyB = 0;
        var overlapDetected = false;

        using var subscriptionA = store.Subscribe(s =>
        {
            if (Interlocked.Increment(ref reentrancyA) > 1)
            {
                overlapDetected = true;
            }

            deliveredA.Enqueue(s.Revision);
            Interlocked.Decrement(ref reentrancyA);
        });

        using var subscriptionB = store.Subscribe(s =>
        {
            if (Interlocked.Increment(ref reentrancyB) > 1)
            {
                overlapDetected = true;
            }

            deliveredB.Enqueue(s.Revision);
            Interlocked.Decrement(ref reentrancyB);
        });

        var producers = Enumerable.Range(0, producerCount).Select(p => Task.Run(() =>
        {
            var sourceId = $"source-{p}";
            for (var seq = 1L; seq <= commitsPerProducer; seq++)
            {
                store.ReplaceSource(CreateSnapshot(sourceId, seq));
            }
        }));

        await Task.WhenAll(producers);

        var finalRevision = store.Current.Revision;
        Assert.Equal(producerCount * commitsPerProducer, finalRevision);

        await WaitUntil(() => !deliveredA.IsEmpty && deliveredA.Last() == finalRevision);
        await WaitUntil(() => !deliveredB.IsEmpty && deliveredB.Last() == finalRevision);

        Assert.False(overlapDetected, "Callback invocations for one subscription must never overlap");
        AssertNonDecreasing(deliveredA.ToArray());
        AssertNonDecreasing(deliveredB.ToArray());
    }

    [Fact]
    public async Task Subscribe_DisposeMidRun_StopsRecordingFurtherRevisions()
    {
        const int commitCount = 500;
        var store = new PolicyStore();
        var delivered = new ConcurrentQueue<long>();
        var subscription = store.Subscribe(s => delivered.Enqueue(s.Revision));
        var sourceId = "source-a";

        var producer = Task.Run(() =>
        {
            for (var seq = 1L; seq <= commitCount; seq++)
            {
                store.ReplaceSource(CreateSnapshot(sourceId, seq));

                if (seq == commitCount / 2)
                {
                    subscription.Dispose();
                }
            }
        });

        await producer;
        await Task.Delay(50); // allow any in-flight delivery to finish

        var countAfterDisposal = delivered.Count;
        await Task.Delay(50);

        Assert.Equal(countAfterDisposal, delivered.Count);
    }

    private static PolicySourceSnapshot CreateSnapshot(string sourceId, long sequence)
    {
        var meta = new PolicySourceMetadata(new SourceRegistrationId(sourceId), PolicySourceKind.File);
        PolicySourceSnapshot.TryCreate(meta, sequence, PolicySourceVersion.Empty, [], out var snapshot, out _);
        return snapshot!;
    }

    private static void AssertNonDecreasing(long[] revisions)
    {
        for (var i = 1; i < revisions.Length; i++)
        {
            Assert.True(revisions[i] >= revisions[i - 1], "Delivered revisions must never run backwards");
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMilliseconds = 10_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMilliseconds)
            {
                Assert.Fail("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }
}
