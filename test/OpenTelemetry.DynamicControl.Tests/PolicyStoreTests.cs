// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Sources;
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
    public void ReplaceSource_FirstSubmission_Applied_RevisionOne()
    {
        var store = new PolicyStore();
        var result = ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.True(result.Applied, "Applied property should be true when status is Applied");
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public void ReplaceSource_SecondDifferentSource_RevisionTwo_BothPresent()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);
        var result = ReplaceEmpty(store, "source-b", PolicySourceKind.OpAmp, sequence: 1);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
        Assert.Equal(2, store.Current.Sources.Length);
    }

    [Fact]
    public void ReplaceSource_ReplaceSourceA_SourceBSnapshotReferenceIdentical()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);
        ReplaceEmpty(store, "source-b", PolicySourceKind.OpAmp, sequence: 1);

        var bBefore = store.Current.TryGetSource(new SourceRegistrationId("source-b"), out var beforeSnapshot)
            ? beforeSnapshot
            : null;

        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 2);

        store.Current.TryGetSource(new SourceRegistrationId("source-b"), out var afterSnapshot);
        Assert.Same(bBefore, afterSnapshot);
    }

    [Fact]
    public void ReplaceSource_Applied_ResultSnapshotIsSameReferenceAsStoreCurrent()
    {
        var store = new PolicyStore();
        var result = ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);

        Assert.Same(store.Current, result.Snapshot);
    }

    [Fact]
    public void Current_ChangesReferenceAfterApply_OldReferenceUnchanged()
    {
        var store = new PolicyStore();
        var before = store.Current;

        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);

        var after = store.Current;
        Assert.NotSame(before, after);
        Assert.Equal(0, before.Revision);
        Assert.Empty(before.Sources);
    }

    [Fact]
    public void ReplaceSource_NonEmptyReplacedByValidEmpty_Applied_SourcePresentAndIsEmpty()
    {
        var store = new PolicyStore();
        var meta = new PolicySourceMetadata(new SourceRegistrationId("source-a"), PolicySourceKind.File);
        var policy = new StubPolicy("trace-sampling", "p1");
        PolicySourceSnapshot.TryCreate(meta, 1, PolicySourceVersion.Empty, [policy], out var full, out _);
        store.ReplaceSource(full!);

        PolicySourceSnapshot.TryCreate(meta, 2, PolicySourceVersion.Empty, [], out var empty, out _);
        var result = store.ReplaceSource(empty!);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
        var found = store.Current.TryGetSource(new SourceRegistrationId("source-a"), out var source);
        Assert.True(found, "source-a should still be present after being replaced with an empty snapshot");
        Assert.True(source!.IsEmpty, "source-a snapshot should be empty after retraction");
    }

    [Fact]
    public void ReplaceSource_EqualSequence_RejectedStaleSequence()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 5);
        var before = store.Current;

        var result = ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 5);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedStaleSequence, result.Status);
        Assert.Equal(1, result.Revision);
        Assert.Same(before, result.Snapshot);
    }

    [Fact]
    public void ReplaceSource_LowerSequence_RejectedStaleSequence()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 5);
        var before = store.Current;

        var result = ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 3);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedStaleSequence, result.Status);
        Assert.Same(before, result.Snapshot);
    }

    [Fact]
    public void ReplaceSource_UnchangedNonNoneVersion_SuppressedUnchangedVersion_RevisionUnchanged()
    {
        var store = new PolicyStore();
        var meta = new PolicySourceMetadata(new SourceRegistrationId("source-a"), PolicySourceKind.File);
        var versionA = new PolicySourceVersion("hash-A");

        ReplaceWithVersion(store, meta, sequence: 5, version: versionA);
        var before = store.Current;
        var sourceBefore = before.TryGetSource(meta.RegistrationId, out var sb) ? sb : null;

        // Resend same version at higher sequence.
        var result = ReplaceWithVersion(store, meta, sequence: 7, version: versionA);

        Assert.Equal(PolicyStoreUpdateStatus.SuppressedUnchangedVersion, result.Status);
        Assert.Equal(1, result.Revision); // unchanged
        Assert.Same(before, result.Snapshot);

        // The source snapshot reference is also unchanged.
        store.Current.TryGetSource(meta.RegistrationId, out var sourceAfter);
        Assert.Same(sourceBefore, sourceAfter);
    }

    [Fact]
    public void ReplaceSource_TwoIdenticalContentSubmissions_VersionNone_BothApplied_RevisionBumpsTwice()
    {
        // With Version.Empty, suppression cannot occur: every submission must be applied.
        var store = new PolicyStore();
        var meta = new PolicySourceMetadata(new SourceRegistrationId("source-a"), PolicySourceKind.File);

        ReplaceEmpty(store, meta.RegistrationId.Value, PolicySourceKind.File, sequence: 1);
        var result = ReplaceEmpty(store, meta.RegistrationId.Value, PolicySourceKind.File, sequence: 2);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
    }

    [Fact]
    public void ReplaceSource_SuppressionAdvancesMaxSequence_LaterLowerSequenceDifferentVersionRejected()
    {
        var store = new PolicyStore();
        var meta = new PolicySourceMetadata(new SourceRegistrationId("source-a"), PolicySourceKind.File);
        var vA = new PolicySourceVersion("hash-A");
        var vB = new PolicySourceVersion("hash-B");

        ReplaceWithVersion(store, meta, sequence: 5, version: vA); // Applied, rev 1
        ReplaceWithVersion(store, meta, sequence: 7, version: vA); // SuppressedUnchangedVersion, max sequence = 7
        var result = ReplaceWithVersion(store, meta, sequence: 6, version: vB); // Must be rejected

        Assert.Equal(PolicyStoreUpdateStatus.RejectedStaleSequence, result.Status);
        Assert.Equal(1, result.Revision); // revision still 1
    }

    [Fact]
    public void ReplaceSource_DifferentPriority_RejectedMetadataMismatch()
    {
        var store = new PolicyStore();
        var id = new SourceRegistrationId("source-a");
        var meta1 = new PolicySourceMetadata(id, PolicySourceKind.File, 5);
        var meta2 = new PolicySourceMetadata(id, PolicySourceKind.File, 99);
        var before = store.Current;

        ReplaceEmpty(store, meta1, sequence: 1);
        var result = ReplaceEmpty(store, meta2, sequence: 2);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedMetadataMismatch, result.Status);
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public void ReplaceSource_DifferentKind_RejectedMetadataMismatch()
    {
        var store = new PolicyStore();
        var id = new SourceRegistrationId("source-a");
        var meta1 = new PolicySourceMetadata(id, PolicySourceKind.File);
        var meta2 = new PolicySourceMetadata(id, PolicySourceKind.Http);

        ReplaceEmpty(store, meta1, sequence: 1);
        var result = ReplaceEmpty(store, meta2, sequence: 2);

        Assert.Equal(PolicyStoreUpdateStatus.RejectedMetadataMismatch, result.Status);
    }

    [Fact]
    public void ReplaceSource_StaleSequenceAndMismatchedMetadata_RejectedMetadataMismatch()
    {
        // Metadata mismatch takes precedence over staleness.
        var store = new PolicyStore();
        var id = new SourceRegistrationId("source-a");
        var meta1 = new PolicySourceMetadata(id, PolicySourceKind.File, 5);
        var meta2 = new PolicySourceMetadata(id, PolicySourceKind.File, 99);

        ReplaceEmpty(store, meta1, sequence: 10);
        var result = ReplaceEmpty(store, meta2, sequence: 1); // stale AND mismatched

        Assert.Equal(PolicyStoreUpdateStatus.RejectedMetadataMismatch, result.Status);
    }

    [Fact]
    public void RemoveSource_PresentSource_Applied_RevisionBumps_SourceGone()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);

        var result = store.RemoveSource(new SourceRegistrationId("source-a"));

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.Equal(2, result.Revision);
        Assert.False(store.Current.TryGetSource(new SourceRegistrationId("source-a"), out _), "removed source should no longer be found in the store");
    }

    [Fact]
    public void RemoveSource_AbsentSource_SourceNotFound_RevisionUnchanged()
    {
        var store = new PolicyStore();

        var result = store.RemoveSource(new SourceRegistrationId("nonexistent"));

        Assert.Equal(PolicyStoreUpdateStatus.SourceNotFound, result.Status);
        Assert.Equal(0, result.Revision);
    }

    [Fact]
    public void RemoveSource_ThenReAddWithLowerSequence_Applied_NoTombstone()
    {
        // After removal there is no maximum sequence, so any sequence >= 1 is accepted.
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 100);
        store.RemoveSource(new SourceRegistrationId("source-a"));

        var result = ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);

        Assert.Equal(PolicyStoreUpdateStatus.Applied, result.Status);
        Assert.True(store.Current.TryGetSource(new SourceRegistrationId("source-a"), out _), "re-added source should be found in the store");
    }

    [Fact]
    public void RemoveSource_OnlySource_RevisionKeepsClimbing_NeverResets()
    {
        var store = new PolicyStore();
        ReplaceEmpty(store, "source-a", PolicySourceKind.File, sequence: 1);
        Assert.Equal(1, store.Current.Revision);

        store.RemoveSource(new SourceRegistrationId("source-a"));
        Assert.Equal(2, store.Current.Revision);
        Assert.Empty(store.Current.Sources);
    }

    [Fact]
    public void ReplaceSource_NullSnapshot_Throws()
    {
        var store = new PolicyStore();
        Assert.Throws<ArgumentNullException>(() => store.ReplaceSource(null!));
    }

    [Fact]
    public void RemoveSource_DefaultRegistrationId_Throws()
    {
        var store = new PolicyStore();
        Assert.Throws<ArgumentException>(() => store.RemoveSource(default));
    }

    [Fact]
    public async Task Concurrency_ReadersDuringWrites_AlwaysSeeInternallyConsistentSnapshot()
    {
        // Verifies the lock-free read guarantee: a snapshot captured via Volatile.Read is
        // always internally consistent. Sources and the lookup are never torn.
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
                var meta = new PolicySourceMetadata(new SourceRegistrationId($"writer-{i}"), PolicySourceKind.File);
                PolicySourceSnapshot.TryCreate(meta, seq, PolicySourceVersion.Empty, [], out var snapshot, out _);
                store.ReplaceSource(snapshot!);
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

                foreach (var source in snapshot.Sources)
                {
                    if (!snapshot.TryGetSource(source.RegistrationId, out var found) || !ReferenceEquals(found, source))
                    {
                        errors.Add($"Sources/lookup desync for {source.RegistrationId.Value}");
                    }
                }

                for (var j = 1; j < snapshot.Sources.Length; j++)
                {
                    if (string.CompareOrdinal(snapshot.Sources[j - 1].RegistrationId.Value, snapshot.Sources[j].RegistrationId.Value) >= 0)
                    {
                        errors.Add("Sources list is not in ordinal order");
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
        const int sourceCount = 5;
        const int writesPerSource = 30;
        var store = new PolicyStore();

        for (var i = 0; i < sourceCount; i++)
        {
            ReplaceEmpty(store, $"source-{i}", PolicySourceKind.File, sequence: 1);
        }

        var before = store.Current;
        var beforeRevision = before.Revision;
        var beforeCount = before.Sources.Length;
        var beforeSources = before.Sources.ToArray();

        var tasks = Enumerable.Range(0, sourceCount)
            .SelectMany(i => Enumerable.Range(2, writesPerSource)
                .Select(seq => Task.Run(() => ReplaceEmpty(store, $"source-{i}", PolicySourceKind.File, sequence: seq))))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(beforeRevision, before.Revision);
        Assert.Equal(beforeCount, before.Sources.Length);
        for (var i = 0; i < beforeSources.Length; i++)
        {
            Assert.Same(beforeSources[i], before.Sources[i]);
        }
    }

    [Fact]
    public async Task Concurrency_ParallelWritesToDistinctSources_FinalRevisionEqualsSourceCount()
    {
        const int sourceCount = 20;
        var store = new PolicyStore();

        var tasks = Enumerable.Range(0, sourceCount).Select(i => Task.Run(() =>
        {
            var meta = new PolicySourceMetadata(new SourceRegistrationId($"source-{i}"), PolicySourceKind.File);
            PolicySourceSnapshot.TryCreate(meta, 1, PolicySourceVersion.Empty, [], out var snapshot, out _);
            store.ReplaceSource(snapshot!);
        }));

        await Task.WhenAll(tasks);

        var final = store.Current;
        Assert.Equal(sourceCount, final.Revision);
        Assert.Equal(sourceCount, final.Sources.Length);

        for (var i = 1; i < final.Sources.Length; i++)
        {
            Assert.True(
                string.CompareOrdinal(final.Sources[i - 1].RegistrationId.Value, final.Sources[i].RegistrationId.Value) < 0,
                "Sources must remain in ordinal RegistrationId order");
        }
    }

    [Fact]
    public async Task Concurrency_ParallelWritesToSingleSource_HighestSequenceApplied_NoTornSnapshot()
    {
        const int attemptCount = 50;
        var store = new PolicyStore();
        var meta = new PolicySourceMetadata(new SourceRegistrationId("source-a"), PolicySourceKind.File);

        // Each task tries to commit with a unique sequence. Sequences are atomically
        // assigned to guarantee uniqueness; the task that commits the highest wins.
        var nextSequence = 0L;

        var tasks = Enumerable.Range(0, attemptCount).Select(i => Task.Run(() =>
        {
            var seq = Interlocked.Increment(ref nextSequence);
            PolicySourceSnapshot.TryCreate(meta, seq, PolicySourceVersion.Empty, [], out var snapshot, out var _);
            store.ReplaceSource(snapshot!);
        }));

        await Task.WhenAll(tasks);

        var final = store.Current;

        // Revision must not exceed the number of attempts (many will be rejected as stale).
        Assert.InRange(final.Revision, 1, attemptCount);
        Assert.Single(final.Sources);

        var sourceSnapshot = final.Sources[0];
        Assert.Equal(meta.RegistrationId, sourceSnapshot.RegistrationId);

        // The highest sequence must win: Interlocked assigns 1..attemptCount, so when
        // sequence attemptCount runs it is always newer than any prior maximum sequence.
        Assert.Equal(attemptCount, sourceSnapshot.Sequence);
    }

    private static PolicyStoreUpdateResult ReplaceEmpty(PolicyStore store, string idValue, PolicySourceKind kind, long sequence)
    {
        var meta = new PolicySourceMetadata(new SourceRegistrationId(idValue), kind);
        return ReplaceEmpty(store, meta, sequence);
    }

    private static PolicyStoreUpdateResult ReplaceEmpty(PolicyStore store, PolicySourceMetadata meta, long sequence)
    {
        PolicySourceSnapshot.TryCreate(meta, sequence, PolicySourceVersion.Empty, [], out var snapshot, out _);
        return store.ReplaceSource(snapshot!);
    }

    private static PolicyStoreUpdateResult ReplaceWithVersion(
        PolicyStore store, PolicySourceMetadata meta, long sequence, PolicySourceVersion version)
    {
        PolicySourceSnapshot.TryCreate(meta, sequence, version, [], out var snapshot, out _);
        return store.ReplaceSource(snapshot!);
    }

    private sealed class StubPolicy : TelemetryPolicy
    {
        public StubPolicy(string policyType, string id)
            : base(id, $"{policyType}/{id}")
        {
            this.PolicyType = policyType;
        }

        public override string PolicyType { get; }
    }
}
