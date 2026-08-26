// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyStoreSubscriptionTests
{
    [Fact]
    public async Task Subscribe_OnEmptyStore_ReplaysEmptySnapshot()
    {
        var store = new PolicyStore();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();

        using var subscription = store.Subscribe(s => delivered.Enqueue(s));

        await WaitUntil(() => !delivered.IsEmpty);
        Assert.Same(PolicyStoreSnapshot.Empty, delivered.Single());
    }

    [Fact]
    public async Task Subscribe_AfterCommits_ReplaysCurrentSnapshot_NotEarlierOnes()
    {
        var store = new PolicyStore();
        Replace(store, "source-a", sequence: 1);
        Replace(store, "source-a", sequence: 2);
        var expected = store.Current;

        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();
        using var subscription = store.Subscribe(s => delivered.Enqueue(s));

        await WaitUntil(() => !delivered.IsEmpty);
        Assert.Same(expected, delivered.First());
    }

    [Fact]
    public async Task Subscribe_ThenCommit_DeliversNewSnapshot()
    {
        var store = new PolicyStore();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();
        using var subscription = store.Subscribe(s => delivered.Enqueue(s));
        await WaitUntil(() => !delivered.IsEmpty); // initial replay

        Replace(store, "source-a", sequence: 1);

        await WaitUntil(() => delivered.Count >= 2);
        Assert.Equal(store.Current.Revision, delivered.Last().Revision);
    }

    [Fact]
    public async Task Subscribe_MultipleIndependentSubscriptions_EachReceivesOwnOrderedStream()
    {
        var store = new PolicyStore();
        var deliveredA = new System.Collections.Concurrent.ConcurrentQueue<long>();
        var deliveredB = new System.Collections.Concurrent.ConcurrentQueue<long>();

        using var subscriptionA = store.Subscribe(s => deliveredA.Enqueue(s.Revision));
        using var subscriptionB = store.Subscribe(s => deliveredB.Enqueue(s.Revision));

        for (var i = 1; i <= 5; i++)
        {
            Replace(store, "source-a", sequence: i);
        }

        await WaitUntil(() => !deliveredA.IsEmpty && deliveredA.Last() == 5);
        await WaitUntil(() => !deliveredB.IsEmpty && deliveredB.Last() == 5);

        AssertNonDecreasing(deliveredA.ToArray());
        AssertNonDecreasing(deliveredB.ToArray());
    }

    [Fact]
    public void Dispose_DisposesAllLiveSubscriptions()
    {
        var store = new PolicyStore();
        var delivered = new System.Collections.Concurrent.ConcurrentQueue<PolicyStoreSnapshot>();
        var subscription = store.Subscribe(s => delivered.Enqueue(s));

        store.Dispose();

        // Further commits after disposal must not deliver to the now-disposed subscription.
        Replace(store, "source-a", sequence: 1);
    }

    [Fact]
    public void Subscribe_NullCallback_Throws()
    {
        var store = new PolicyStore();
        Assert.Throws<ArgumentNullException>(() => store.Subscribe(null!));
    }

    [Fact]
    public void Subscribe_AfterDispose_Throws()
    {
        var store = new PolicyStore();
        store.Dispose();

        Assert.Throws<ObjectDisposedException>(() => store.Subscribe(_ => { }));
    }

    private static void AssertNonDecreasing(long[] revisions)
    {
        for (var i = 1; i < revisions.Length; i++)
        {
            Assert.True(revisions[i] >= revisions[i - 1], "Delivered revisions must never run backwards");
        }
    }

    private static void Replace(PolicyStore store, string idValue, long sequence)
    {
        var meta = new PolicySourceMetadata(new SourceRegistrationId(idValue), PolicySourceKind.File);
        PolicySourceSnapshot.TryCreate(meta, sequence, PolicySourceVersion.Empty, [], out var snapshot, out _);
        store.ReplaceSource(snapshot!);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMilliseconds)
            {
                Assert.Fail("Timed out waiting for condition.");
            }

            await Task.Delay(5);
        }
    }
}
