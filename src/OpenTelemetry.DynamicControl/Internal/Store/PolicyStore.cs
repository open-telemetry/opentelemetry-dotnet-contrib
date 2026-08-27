// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// Stores the current set of per-source policy snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Reads are lock-free. Callers must capture <see cref="Current"/> once
/// and work from that instance; re-reading mid-operation can observe a newer revision
/// and produce incorrect results.
/// </para>
/// <para>
/// Updates are serialized under a single lock. Each accepted change builds and publishes
/// a new <see cref="PolicyStoreSnapshot"/>.
/// </para>
/// </remarks>
internal sealed class PolicyStore : IDisposable
{
    private readonly Lock updateLock = new();

    private readonly Dictionary<SourceRegistrationId, PolicySourceSnapshot> sources = [];

    // Suppressed submissions still advance the sequence used for staleness checks.
    private readonly Dictionary<SourceRegistrationId, long> maxSequence = [];

    private readonly PolicyChangeNotifier notifier = new();

    private PolicyStoreSnapshot current = PolicyStoreSnapshot.Empty;

    /// <summary>
    /// Gets the current store snapshot.
    /// </summary>
    /// <remarks>
    /// Capture the snapshot once per operation to avoid mixing revisions.
    /// </remarks>
    public PolicyStoreSnapshot Current => Volatile.Read(ref this.current);

    /// <summary>
    /// Subscribes to policy changes, starting with the current snapshot.
    /// </summary>
    /// <remarks>
    /// Registration does not miss concurrent updates. Delivery follows the guarantees
    /// documented by <see cref="PolicyChangeSubscription"/>.
    /// </remarks>
    /// <param name="onChanged">The callback to invoke with each delivered snapshot.</param>
    /// <returns>A handle that stops delivery when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onChanged"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The store has been disposed.</exception>
    public IDisposable Subscribe(Action<PolicyStoreSnapshot> onChanged)
    {
        Guard.ThrowIfNull(onChanged);

        PolicyChangeSubscription subscription;
        PolicyStoreSnapshot initialSnapshot;

        lock (this.updateLock)
        {
            // Registration and snapshot capture must be atomic with respect to publication.
            subscription = this.notifier.Add(onChanged);
            initialSnapshot = this.current;
        }

        subscription.Enqueue(initialSnapshot);
        return subscription;
    }

    /// <summary>
    /// Stops all subscriptions without waiting for callbacks already in progress.
    /// </summary>
    public void Dispose() => this.notifier.Dispose();

    /// <summary>
    /// Replaces the policy set for the source described by <paramref name="snapshot"/>.
    /// </summary>
    /// <param name="snapshot">The new snapshot to commit.</param>
    /// <returns>The outcome of the submission and the current (resulting or unchanged) snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    public PolicyStoreUpdateResult ReplaceSource(PolicySourceSnapshot snapshot)
    {
        Guard.ThrowIfNull(snapshot);

        var id = snapshot.RegistrationId;
        PolicyStoreSnapshot newSnapshot;
        ReadOnlySpan<PolicyChangeSubscription> subscribers;

        lock (this.updateLock)
        {
            if (this.sources.TryGetValue(id, out var existing)
                && !existing.Metadata.Equals(snapshot.Metadata))
            {
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.RejectedMetadataMismatch, this.current);
            }

            if (this.maxSequence.TryGetValue(id, out var maxSeq)
                && snapshot.Sequence <= maxSeq)
            {
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.RejectedStaleSequence, this.current);
            }

            if (!snapshot.Version.IsEmpty
                && existing != null
                && snapshot.Version.Equals(existing.Version))
            {
                this.maxSequence[id] = snapshot.Sequence;
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.SuppressedUnchangedVersion, this.current);
            }

            this.sources[id] = snapshot;
            this.maxSequence[id] = snapshot.Sequence;

            newSnapshot = new PolicyStoreSnapshot(this.current.Revision + 1, this.sources);
            Volatile.Write(ref this.current, newSnapshot);

            subscribers = this.notifier.Subscribers;
        }

        Notify(subscribers, newSnapshot);

        return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.Applied, newSnapshot);
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

        PolicyStoreSnapshot newSnapshot;
        ReadOnlySpan<PolicyChangeSubscription> subscribers;

        lock (this.updateLock)
        {
            if (!this.sources.Remove(registrationId))
            {
                return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.SourceNotFound, this.current);
            }

            this.maxSequence.Remove(registrationId);

            newSnapshot = new PolicyStoreSnapshot(this.current.Revision + 1, this.sources);
            Volatile.Write(ref this.current, newSnapshot);

            subscribers = this.notifier.Subscribers;
        }

        Notify(subscribers, newSnapshot);

        return new PolicyStoreUpdateResult(PolicyStoreUpdateStatus.Applied, newSnapshot);
    }

    private static void Notify(ReadOnlySpan<PolicyChangeSubscription> subscribers, PolicyStoreSnapshot snapshot)
    {
        foreach (var subscription in subscribers)
        {
            subscription.Enqueue(snapshot);
        }
    }
}
