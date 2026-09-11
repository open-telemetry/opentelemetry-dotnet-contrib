// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// The outcome of a single submission to the policy store, together with the resulting
/// (or unchanged) snapshot.
/// </summary>
/// <remarks>
/// The snapshot is always the current published value>. For rejected and suppressed
/// submissions it is the same reference that was already published before the call.
/// </remarks>
internal readonly struct PolicyStoreUpdateResult
{
    internal PolicyStoreUpdateResult(PolicyStoreUpdateStatus status, PolicyStoreSnapshot snapshot)
    {
        this.Status = status;
        this.Snapshot = snapshot;
    }

    /// <summary>
    /// Gets the status of the submission.
    /// </summary>
    public PolicyStoreUpdateStatus Status { get; }

    /// <summary>
    /// Gets the current store snapshot, whether or not the submission was applied.
    /// </summary>
    public PolicyStoreSnapshot Snapshot { get; }

    /// <summary>
    /// Gets a value indicating whether the submission was applied and the revision advanced.
    /// </summary>
    public bool Applied => this.Status == PolicyStoreUpdateStatus.Applied;

    /// <summary>
    /// Gets the current store revision.
    /// </summary>
    public long Revision => this.Snapshot.Revision;
}
