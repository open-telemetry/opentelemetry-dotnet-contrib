// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Diagnostics;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// Represents a subscription to policy changes.
/// </summary>
/// <remarks>
/// Callbacks are asynchronous and serialized per subscription. Delivered revisions never
/// decrease, but pending notifications may be coalesced to the newest snapshot. Changes
/// triggered by a callback are delivered after that callback completes. Callback exceptions
/// are reported through <see cref="DynamicControlEventSource"/> and do not stop subsequent
/// delivery.
/// </remarks>
internal sealed class PolicyChangeSubscription : IDisposable
{
    private readonly Lock gate = new();

    private Action<PolicyStoreSnapshot>? onChanged;
    private PolicyChangeNotifier? owner;
    private PolicyStoreSnapshot? pending;
    private long lastSeenRevision = -1;
    private bool dispatching;
    private bool disposed;

    internal PolicyChangeSubscription(Action<PolicyStoreSnapshot> onChanged, PolicyChangeNotifier owner)
    {
        this.onChanged = onChanged;
        this.owner = owner;
    }

    /// <summary>
    /// Unregisters the subscription and discards pending delivery. A callback already in
    /// progress may complete.
    /// </summary>
    public void Dispose()
    {
        PolicyChangeNotifier? owner;

        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.pending = null;
            this.onChanged = null;
            owner = this.owner;
            this.owner = null;
        }

        owner?.Remove(this);
    }

    /// <summary>
    /// Queues a snapshot for delivery. A newer snapshot may replace one that is still pending.
    /// </summary>
    /// <param name="snapshot">The snapshot to deliver.</param>
    internal void Enqueue(PolicyStoreSnapshot snapshot)
    {
        lock (this.gate)
        {
            // Concurrent notifications can arrive out of order; never deliver an older revision.
            if (this.disposed || snapshot.Revision <= this.lastSeenRevision)
            {
                return;
            }

            this.lastSeenRevision = snapshot.Revision;

            // Coalesce rather than queue every revision or block the committer: the
            // telemetry-policy OTEP requires async, fail-open updates, so dropping a
            // superseded revision here is intentional, not a bug.
            this.pending = snapshot;

            if (this.dispatching)
            {
                return;
            }

            this.dispatching = true;
        }

        ThreadPool.UnsafeQueueUserWorkItem(static state => ((PolicyChangeSubscription?)state!).Drain(), this);
    }

    private void Drain()
    {
        while (true)
        {
            PolicyStoreSnapshot? snapshot;
            Action<PolicyStoreSnapshot>? onChanged;

            lock (this.gate)
            {
                onChanged = this.onChanged;
                if (this.disposed || this.pending is null || onChanged is null)
                {
                    this.dispatching = false;
                    return;
                }

                snapshot = this.pending;
                this.pending = null;
            }

            try
            {
                onChanged(snapshot);
            }
            catch (Exception ex)
            {
                DynamicControlEventSource.Log.PolicyChangeSubscriberException(ex);
            }
        }
    }
}
