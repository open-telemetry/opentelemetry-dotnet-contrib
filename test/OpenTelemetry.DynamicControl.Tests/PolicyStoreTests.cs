// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Tests;

public class PolicyStoreTests
{
    [Fact]
    public void NewStore_CurrentIsEmpty_RevisionZero()
    {
        var store = new PolicyStore();
        Assert.Same(PolicyStoreSnapshot.Empty, store.Current);
        Assert.Equal(0, store.Current.Revision);
    }

    [Fact]
    public void Current_RepeatedReadsWithNoUpdates_ReturnsSameReference()
    {
        var store = new PolicyStore();
        var first = store.Current;
        var second = store.Current;
        Assert.Same(first, second);
    }

    [Fact]
    public void ReplaceProvider_FirstSubmission_Applied_RevisionOne()
    {
        var store = new PolicyStore();
        var result = ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.True(result.Applied, "Applied property should be true when status is Applied");
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public void ReplaceProvider_SecondDifferentProvider_RevisionTwo_BothPresent()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);
        var result = ReplaceEmpty(store, "provider-b", PolicyProviderKind.OpAmp, sequence: 1);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
        Assert.Equal(2, store.Current.Providers.Length);
    }

    [Fact]
    public void ReplaceProvider_ReplaceProviderA_ProviderBSnapshotReferenceIdentical()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);
        ReplaceEmpty(store, "provider-b", PolicyProviderKind.OpAmp, sequence: 1);

        var bBefore = store.Current.TryGetProvider(new ProviderRegistrationId("provider-b"), out var beforeSnapshot)
            ? beforeSnapshot
            : null;

        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 2);

        store.Current.TryGetProvider(new ProviderRegistrationId("provider-b"), out var afterSnapshot);
        Assert.Same(bBefore, afterSnapshot);
    }

    [Fact]
    public void ReplaceProvider_Applied_ResultSnapshotIsSameReferenceAsStoreCurrent()
    {
        var store = new PolicyStore();
        var result = ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);

        Assert.Same(store.Current, result.Snapshot);
    }

    [Fact]
    public void Current_ChangesReferenceAfterApply_OldReferenceUnchanged()
    {
        var store = new PolicyStore();
        var before = store.Current;

        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);

        var after = store.Current;
        Assert.NotSame(before, after);
        Assert.Equal(0, before.Revision);
        Assert.Empty(before.Providers);
    }

    [Fact]
    public void ReplaceProvider_NonEmptyReplacedByValidEmpty_Applied_ProviderPresentAndIsEmpty()
    {
        var store = new PolicyStore();
        var meta = new PolicyProviderMetadata(new ProviderRegistrationId("provider-a"), PolicyProviderKind.File);
        var policy = new StubPolicy("trace-sampling", "p1");
        PolicyProviderSnapshot.TryCreate(meta, 1, PolicyProviderVersion.Empty, [policy], out var full, out _);
        store.ReplaceProvider(full!);

        PolicyProviderSnapshot.TryCreate(meta, 2, PolicyProviderVersion.Empty, [], out var empty, out _);
        var result = store.ReplaceProvider(empty!);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
        var found = store.Current.TryGetProvider(new ProviderRegistrationId("provider-a"), out var provider);
        Assert.True(found, "provider-a should still be present after being replaced with an empty snapshot");
        Assert.True(provider!.IsEmpty, "provider-a snapshot should be empty after retraction");
    }

    [Fact]
    public void ReplaceProvider_EqualSequence_RejectedStaleSequence()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 5);
        var before = store.Current;

        var result = ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 5);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedStaleSequence, result.Status);
        Assert.Equal(1, result.Revision);
        Assert.Same(before, result.Snapshot);
    }

    [Fact]
    public void ReplaceProvider_LowerSequence_RejectedStaleSequence()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 5);
        var before = store.Current;

        var result = ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 3);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedStaleSequence, result.Status);
        Assert.Same(before, result.Snapshot);
    }

    [Fact]
    public void ReplaceProvider_UnchangedNonNoneVersion_SuppressedUnchangedVersion_RevisionUnchanged()
    {
        var store = new PolicyStore();
        var meta = new PolicyProviderMetadata(new ProviderRegistrationId("provider-a"), PolicyProviderKind.File);
        var versionA = new PolicyProviderVersion("hash-A");

        ReplaceWithVersion(store, meta, sequence: 5, version: versionA);
        var before = store.Current;
        var providerBefore = before.TryGetProvider(meta.RegistrationId, out var pb) ? pb : null;

        // Resend same version at higher sequence.
        var result = ReplaceWithVersion(store, meta, sequence: 7, version: versionA);

        Assert.Equal(PolicyStoreUpdateStatus.SuppressedUnchangedVersion, result.Status);
        Assert.Equal(1, result.Revision); // unchanged
        Assert.Same(before, result.Snapshot);

        // The provider snapshot reference is also unchanged.
        store.Current.TryGetProvider(meta.RegistrationId, out var providerAfter);
        Assert.Same(providerBefore, providerAfter);
    }

    [Fact]
    public void ReplaceProvider_TwoIdenticalContentSubmissions_VersionNone_BothApplied_RevisionBumpsTwice()
    {
        // With Version.Empty, suppression cannot occur: every submission must be applied.
        var store = new PolicyStore();
        var meta = new PolicyProviderMetadata(new ProviderRegistrationId("provider-a"), PolicyProviderKind.File);

        ReplaceEmpty(store, meta.RegistrationId.Value, PolicyProviderKind.File, sequence: 1);
        var result = ReplaceEmpty(store, meta.RegistrationId.Value, PolicyProviderKind.File, sequence: 2);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
    }

    [Fact]
    public void ReplaceProvider_SuppressionAdvancesMaxSequence_LaterLowerSequenceDifferentVersionRejected()
    {
        var store = new PolicyStore();
        var meta = new PolicyProviderMetadata(new ProviderRegistrationId("provider-a"), PolicyProviderKind.File);
        var vA = new PolicyProviderVersion("hash-A");
        var vB = new PolicyProviderVersion("hash-B");

        ReplaceWithVersion(store, meta, sequence: 5, version: vA); // Applied, rev 1
        ReplaceWithVersion(store, meta, sequence: 7, version: vA); // SuppressedUnchangedVersion, max sequence = 7
        var result = ReplaceWithVersion(store, meta, sequence: 6, version: vB); // Must be rejected

        Assert.Equal(PolicyStoreUpdateStatus.RejectedStaleSequence, result.Status);
        Assert.Equal(1, result.Revision); // revision still 1
    }

    [Fact]
    public void ReplaceProvider_DifferentPriority_RejectedMetadataMismatch()
    {
        var store = new PolicyStore();
        var id = new ProviderRegistrationId("provider-a");
        var meta1 = new PolicyProviderMetadata(id, PolicyProviderKind.File, 5);
        var meta2 = new PolicyProviderMetadata(id, PolicyProviderKind.File, 99);
        _ = store.Current;

        ReplaceEmpty(store, meta1, sequence: 1);
        var result = ReplaceEmpty(store, meta2, sequence: 2);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedMetadataMismatch, result.Status);
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public void ReplaceProvider_DifferentKind_RejectedMetadataMismatch()
    {
        var store = new PolicyStore();
        var id = new ProviderRegistrationId("provider-a");
        var meta1 = new PolicyProviderMetadata(id, PolicyProviderKind.File);
        var meta2 = new PolicyProviderMetadata(id, PolicyProviderKind.Http);

        ReplaceEmpty(store, meta1, sequence: 1);
        var result = ReplaceEmpty(store, meta2, sequence: 2);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedMetadataMismatch, result.Status);
    }

    [Fact]
    public void ReplaceProvider_StaleSequenceAndMismatchedMetadata_RejectedMetadataMismatch()
    {
        // Metadata mismatch takes precedence over staleness.
        var store = new PolicyStore();
        var id = new ProviderRegistrationId("provider-a");
        var meta1 = new PolicyProviderMetadata(id, PolicyProviderKind.File, 5);
        var meta2 = new PolicyProviderMetadata(id, PolicyProviderKind.File, 99);

        ReplaceEmpty(store, meta1, sequence: 10);
        var result = ReplaceEmpty(store, meta2, sequence: 1); // stale AND mismatched

        Assert.Equal(PolicyStoreUpdateStatus.RejectedMetadataMismatch, result.Status);
    }

    [Fact]
    public void RemoveProvider_PresentProvider_Applied_RevisionBumps_ProviderGone()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);

        var result = store.RemoveProvider(new ProviderRegistrationId("provider-a"));

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
        Assert.False(store.Current.TryGetProvider(new ProviderRegistrationId("provider-a"), out _), "removed provider should no longer be found in the store");
    }

    [Fact]
    public void RemoveProvider_AbsentProvider_ProviderNotFound_RevisionUnchanged()
    {
        var store = new PolicyStore();

        var result = store.RemoveProvider(new ProviderRegistrationId("nonexistent"));

        Assert.Equal(PolicyStoreUpdateStatus.ProviderNotFound, result.Status);
        Assert.Equal(0, result.Revision);
    }

    [Fact]
    public void RemoveProvider_ThenReAddWithLowerSequence_Applied_NoTombstone()
    {
        // After removal there is no maximum sequence, so any sequence >= 1 is accepted.
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 100);
        store.RemoveProvider(new ProviderRegistrationId("provider-a"));

        var result = ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.True(store.Current.TryGetProvider(new ProviderRegistrationId("provider-a"), out _), "re-added provider should be found in the store");
    }

    [Fact]
    public void RemoveProvider_OnlyProvider_RevisionKeepsClimbing_NeverResets()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "provider-a", PolicyProviderKind.File, sequence: 1);
        Assert.Equal(1, store.Current.Revision);

        store.RemoveProvider(new ProviderRegistrationId("provider-a"));
        Assert.Equal(2, store.Current.Revision);
        Assert.Empty(store.Current.Providers);
    }

    [Fact]
    public void ReplaceProvider_NullSnapshot_Throws()
    {
        var store = new PolicyStore();
        Assert.Throws<ArgumentNullException>(() => store.ReplaceProvider(null!));
    }

    [Fact]
    public void RemoveProvider_DefaultRegistrationId_Throws()
    {
        var store = new PolicyStore();
        Assert.Throws<ArgumentException>(() => store.RemoveProvider(default));
    }

    [Fact]
    public async Task Concurrency_ReadersDuringWrites_AlwaysSeeInternallyConsistentSnapshot()
    {
        // Verifies the lock-free read guarantee: a snapshot captured via Volatile.Read is
        // always internally consistent. Providers and the lookup are never torn.
        const int writerCount = 10;
        const int readerCount = 10;
        const int writesPerWriter = 50;
        const int readsPerReader = 200;
        var store = new PolicyStore();
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

        var writerTasks = Enumerable.Range(0, writerCount).Select(i => Task.Run(() =>
        {
            for (var seq = 1L; seq <= writesPerWriter; seq++)
            {
                var meta = new PolicyProviderMetadata(new ProviderRegistrationId($"writer-{i}"), PolicyProviderKind.File);
                PolicyProviderSnapshot.TryCreate(meta, seq, PolicyProviderVersion.Empty, [], out var snapshot, out _);
                store.ReplaceProvider(snapshot!);
            }
        }));

        var readerTasks = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < readsPerReader; i++)
            {
                var snapshot = store.Current;

                if (snapshot.Revision < 0)
                {
                    errors.Add($"Negative revision: {snapshot.Revision}");
                }

                foreach (var provider in snapshot.Providers)
                {
                    if (!snapshot.TryGetProvider(provider.RegistrationId, out var found) || !ReferenceEquals(found, provider))
                    {
                        errors.Add($"Providers/lookup desync for {provider.RegistrationId.Value}");
                    }
                }

                for (var j = 1; j < snapshot.Providers.Length; j++)
                {
                    if (string.CompareOrdinal(snapshot.Providers[j - 1].RegistrationId.Value, snapshot.Providers[j].RegistrationId.Value) >= 0)
                    {
                        errors.Add("Providers list is not in ordinal order");
                    }
                }
            }
        }));

        await Task.WhenAll(writerTasks.Concat(readerTasks));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Concurrency_CapturedSnapshot_RemainsStableWhileConcurrentWritesOccur()
    {
        // Verifies the copy-on-write guarantee: a snapshot reference captured before a burst
        // of writes is never mutated by those writes.
        const int providerCount = 5;
        const int writesPerProvider = 30;
        var store = new PolicyStore();

        for (var i = 0; i < providerCount; i++)
        {
            ReplaceEmpty(store, $"provider-{i}", PolicyProviderKind.File, sequence: 1);
        }

        var before = store.Current;
        var beforeRevision = before.Revision;
        var beforeCount = before.Providers.Length;
        var beforeProviders = before.Providers.ToArray();

        var tasks = Enumerable.Range(0, providerCount)
            .SelectMany(i => Enumerable.Range(2, writesPerProvider)
                .Select(seq => Task.Run(() => ReplaceEmpty(store, $"provider-{i}", PolicyProviderKind.File, sequence: seq))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(beforeRevision, before.Revision);
        Assert.Equal(beforeCount, before.Providers.Length);
        for (var i = 0; i < beforeProviders.Length; i++)
        {
            Assert.Same(beforeProviders[i], before.Providers[i]);
        }
    }

    [Fact]
    public async Task Concurrency_ParallelWritesToDistinctProviders_FinalRevisionEqualsProviderCount()
    {
        const int providerCount = 20;
        var store = new PolicyStore();

        var tasks = Enumerable.Range(0, providerCount).Select(i => Task.Run(() =>
        {
            var meta = new PolicyProviderMetadata(new ProviderRegistrationId($"provider-{i}"), PolicyProviderKind.File);
            PolicyProviderSnapshot.TryCreate(meta, 1, PolicyProviderVersion.Empty, [], out var snapshot, out _);
            store.ReplaceProvider(snapshot!);
        }));

        await Task.WhenAll(tasks);

        var final = store.Current;
        Assert.Equal(providerCount, final.Revision);
        Assert.Equal(providerCount, final.Providers.Length);

        for (var i = 1; i < final.Providers.Length; i++)
        {
            Assert.True(
                string.CompareOrdinal(final.Providers[i - 1].RegistrationId.Value, final.Providers[i].RegistrationId.Value) < 0,
                "Providers must remain in ordinal RegistrationId order");
        }
    }

    [Fact]
    public async Task Concurrency_ParallelWritesToSingleProvider_HighestSequenceApplied_NoTornSnapshot()
    {
        const int attemptCount = 50;
        var store = new PolicyStore();
        var meta = new PolicyProviderMetadata(new ProviderRegistrationId("provider-a"), PolicyProviderKind.File);

        // Each task tries to commit with a unique sequence. Sequences are atomically
        // assigned to guarantee uniqueness; the task that commits the highest wins.
        var nextSequence = 0L;

        var tasks = Enumerable.Range(0, attemptCount).Select(i => Task.Run(() =>
        {
            var seq = Interlocked.Increment(ref nextSequence);
            PolicyProviderSnapshot.TryCreate(meta, seq, PolicyProviderVersion.Empty, [], out var snapshot, out var _);
            store.ReplaceProvider(snapshot!);
        }));

        await Task.WhenAll(tasks);

        var final = store.Current;

        // Revision must not exceed the number of attempts (many will be rejected as stale).
        Assert.InRange(final.Revision, 1, attemptCount);
        Assert.Single(final.Providers);

        var providerSnapshot = final.Providers[0];
        Assert.Equal(meta.RegistrationId, providerSnapshot.RegistrationId);

        // The highest sequence must win: Interlocked assigns 1..attemptCount, so when
        // sequence attemptCount runs it is always newer than any prior maximum sequence.
        Assert.Equal(attemptCount, providerSnapshot.Sequence);
    }

    private static PolicyStoreUpdateResult ReplaceEmpty(PolicyStore store, string idValue, PolicyProviderKind kind, long sequence)
    {
        var meta = new PolicyProviderMetadata(new ProviderRegistrationId(idValue), kind);
        return ReplaceEmpty(store, meta, sequence);
    }

    private static PolicyStoreUpdateResult ReplaceEmpty(PolicyStore store, PolicyProviderMetadata meta, long sequence)
    {
        PolicyProviderSnapshot.TryCreate(meta, sequence, PolicyProviderVersion.Empty, [], out var snapshot, out _);
        return store.ReplaceProvider(snapshot!);
    }

    private static PolicyStoreUpdateResult ReplaceWithVersion(
        PolicyStore store, PolicyProviderMetadata meta, long sequence, PolicyProviderVersion version)
    {
        PolicyProviderSnapshot.TryCreate(meta, sequence, version, [], out var snapshot, out _);
        return store.ReplaceProvider(snapshot!);
    }

    private sealed class StubPolicy : TelemetryPolicy
    {
        public StubPolicy(string policyType, string id)
            : base(new PolicyId(id), $"{policyType}/{id}")
        {
            this.PolicyType = new PolicyType(policyType);
        }

        public override PolicyType PolicyType { get; }
    }
}
