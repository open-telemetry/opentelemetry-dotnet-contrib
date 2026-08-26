// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// Owns the set of subscriptions registered against a <see cref="PolicyStore"/> and
/// dispatches published snapshots to them.
/// </summary>
internal sealed class PolicyChangeNotifier : IDisposable
{
    private static readonly PolicyChangeSubscription[] EmptySubscribers = [];

    private readonly Lock registrationLock = new();

    // The subscriber list is a copy-on-write array.
    private PolicyChangeSubscription[] subscribers = EmptySubscribers;
    private bool disposed;

    /// <summary>
    /// Gets a read-only view of the currently registered subscriptions.
    /// </summary>
    internal ReadOnlySpan<PolicyChangeSubscription> Subscribers =>
        Volatile.Read(ref this.subscribers); // The array is copy-on-write, so it is safe to read without a lock.

    /// <summary>
    /// Disposes every currently registered subscription.
    /// </summary>
    public void Dispose()
    {
        PolicyChangeSubscription[] current;

        lock (this.registrationLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            current = this.subscribers;
            Volatile.Write(ref this.subscribers, EmptySubscribers);
        }

        foreach (var subscription in current)
        {
            subscription.Dispose();
        }
    }

    /// <summary>
    /// Registers a new subscription.
    /// </summary>
    /// <param name="onChanged">The callback to invoke with each delivered snapshot.</param>
    /// <returns>The new subscription. Disposing it stops future delivery and removes it from this notifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onChanged"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The notifier has been disposed.</exception>
    internal PolicyChangeSubscription Add(Action<PolicyStoreSnapshot> onChanged)
    {
        Guard.ThrowIfNull(onChanged);

        lock (this.registrationLock)
        {
            this.ThrowIfDisposed();

            var subscription = new PolicyChangeSubscription(onChanged, this);
            var current = this.subscribers;
            var updated = new PolicyChangeSubscription[current.Length + 1];
            Array.Copy(current, updated, current.Length);
            updated[current.Length] = subscription;
            Volatile.Write(ref this.subscribers, updated);

            return subscription;
        }
    }

    /// <summary>
    /// Removes a subscription. Called by <see cref="PolicyChangeSubscription.Dispose"/>.
    /// </summary>
    /// <param name="subscription">The subscription to remove.</param>
    internal void Remove(PolicyChangeSubscription subscription)
    {
        lock (this.registrationLock)
        {
            var current = this.subscribers;
            var index = Array.IndexOf(current, subscription);
            if (index < 0)
            {
                return;
            }

            if (current.Length == 1)
            {
                Volatile.Write(ref this.subscribers, EmptySubscribers);
                return;
            }

            var updated = new PolicyChangeSubscription[current.Length - 1];
            Array.Copy(current, updated, index);
            Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
            Volatile.Write(ref this.subscribers, updated);
        }
    }

#if NET
    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(this.disposed, this);
#else
    private void ThrowIfDisposed()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(PolicyChangeNotifier));
        }
    }
#endif
}
