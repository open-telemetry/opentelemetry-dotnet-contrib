// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// A copy-on-write holder for the complete set of per-source policy snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Reads are lock-free: <see cref="Current"/> reads via <see cref="Volatile.Read{T}"/>
/// and returns a point-in-time value. Callers must capture <see cref="Current"/> once
/// and work from that instance; re-reading mid-operation can observe a newer revision
/// and produce incorrect results.
/// </para>
/// <para>
/// Updates are serialized under a single lock. Each accepted change builds and publishes
/// a new <see cref="PolicyStoreSnapshot"/>, reusing unchanged
/// <see cref="PolicySourceSnapshot"/> instances by reference. The cost of a commit is
/// proportional to the number of sources, not the number of policies.
/// </para>
/// </remarks>
internal sealed class PolicyStore
{
    private readonly Lock updateLock = new();

    // Mutable working set.
    private readonly Dictionary<SourceRegistrationId, PolicySourceSnapshot> sources = [];

    // Maximum sequence seen per source. Governs staleness for both Applied and
    // SuppressedUnchangedVersion outcomes. Separate from the snapshot so suppression
    // can advance the sequence without altering the published state.
    private readonly Dictionary<SourceRegistrationId, long> maxSequence = [];

    // The currently published snapshot.
    private PolicyStoreSnapshot current = PolicyStoreSnapshot.Empty;

    /// <summary>
    /// Gets the current store snapshot.
    /// </summary>
    /// <remarks>
    /// This is a point-in-time value. Capture it once and work from that instance;
    /// re-reading this property mid-operation can observe a newer revision published
    /// by a concurrent update and produce incorrect results.
    /// </remarks>
    public PolicyStoreSnapshot Current => Volatile.Read(ref this.current);

    /// <summary>
    /// Replaces the policy set for the source described by <paramref name="snapshot"/>.
    /// The submission is evaluated against metadata consistency, sequence staleness, and
    /// version suppression gates before being applied.
    /// </summary>
    /// <param name="snapshot">The new snapshot to commit.</param>
    /// <returns>The outcome of the submission and the current (resulting or unchanged) snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public PolicyStoreUpdateResult ReplaceSource(PolicySourceSnapshot snapshot)
    {
        Guard.ThrowIfNull(snapshot);

        var id = snapshot.RegistrationId;

        lock (this.updateLock)
        {
            // Gate 1: metadata mismatch. Checked before staleness so a configuration
            // defect surfaces even when the offending submission is also stale.
            if (this.sources.TryGetValue(id, out var existing)
                && !existing.Metadata.Equals(snapshot.Metadata))
            {
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.RejectedMetadataMismatch, this.current);
            }

            // Gate 2: staleness.
            if (this.maxSequence.TryGetValue(id, out var maxSeq)
                && snapshot.Sequence <= maxSeq)
            {
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.RejectedStaleSequence, this.current);
            }

            // Gate 3: version suppression. Advance the maximum sequence even on
            // suppression to prevent a lower-sequence later submission from winning.
            if (!snapshot.Version.IsEmpty
                && existing != null
                && snapshot.Version.Equals(existing.Version))
            {
                // Gate 2 above guarantees snapshot.Sequence > maxSeq; this assignment always increases the value.
                this.maxSequence[id] = snapshot.Sequence;
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.SuppressedUnchangedVersion, this.current);
            }

            // Apply: replace the source entry, advance the maximum sequence, and
            // publish a new snapshot.
            this.sources[id] = snapshot;
            this.maxSequence[id] = snapshot.Sequence;

            var newSnapshot = new PolicyStoreSnapshot(this.current.Revision + 1, this.sources);
            Volatile.Write(ref this.current, newSnapshot);

            return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.Applied, newSnapshot);
        }
    }

    /// <summary>
    /// Removes the source identified by <paramref name="registrationId"/> from the store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a configuration lifecycle operation. It must not be called in response to
    /// a transport disconnect; a disconnect is not a retraction, and calling
    /// <see cref="RemoveSource"/> in response to one would clear the effective policies
    /// and leave downstream samplers without configuration until the next connection.
    /// </para>
    /// <para>
    /// After removal, no deletion record is retained. Re-adding the same
    /// <see cref="SourceRegistrationId"/> starts fresh and accepts any sequence
    /// greater than or equal to 1, including one lower than a previously accepted value.
    /// </para>
    /// </remarks>
    /// <param name="registrationId">The identity of the source to remove. Must not be <see cref="SourceRegistrationId.Empty"/>.</param>
    /// <returns>The outcome of the removal and the current (resulting or unchanged) snapshot.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="registrationId"/> is the default value.</exception>
    public PolicyStoreUpdateResult RemoveSource(SourceRegistrationId registrationId)
    {
        Guard.ThrowIfDefault(registrationId);

        lock (this.updateLock)
        {
            if (!this.sources.Remove(registrationId))
            {
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.SourceNotFound, this.current);
            }

            this.maxSequence.Remove(registrationId);

            var newSnapshot = new PolicyStoreSnapshot(this.current.Revision + 1, this.sources);
            Volatile.Write(ref this.current, newSnapshot);

            return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.Applied, newSnapshot);
        }
    }
}
