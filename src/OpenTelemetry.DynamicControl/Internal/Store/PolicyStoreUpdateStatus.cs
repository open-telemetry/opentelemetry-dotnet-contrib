// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// Describes the outcome of a single submission to the policy store.
/// </summary>
internal enum PolicyStoreUpdateStatus
{
    /// <summary>
    /// Default/uninitialized value. This member should not appear in production paths.
    /// </summary>
    None = 0,

    /// <summary>
    /// The submission was accepted. The store's revision has advanced by one and a new
    /// <see cref="PolicyStoreSnapshot"/> has been published.
    /// </summary>
    Applied = 1,

    /// <summary>
    /// The submission's sequence number was less than or equal to the highest sequence
    /// already accepted or suppressed for this source. The newer set survives and the revision
    /// is unchanged.
    /// </summary>
    RejectedStaleSequence = 2,

    /// <summary>
    /// The submission's <see cref="PolicySourceVersion"/> matched the version currently
    /// effective for this source, so the store state has not changed. The revision is
    /// unchanged, but the source's maximum sequence has advanced to prevent a later,
    /// lower-sequence submission from replacing the current set.
    /// </summary>
    SuppressedUnchangedVersion = 3,

    /// <summary>
    /// The submission's metadata differed from the metadata pinned at the source's first
    /// accepted update. The store state is unchanged.
    /// </summary>
    RejectedMetadataMismatch = 4,

    /// <summary>
    /// A <see cref="PolicyStore.RemoveSource"/> call named a source that is not currently
    /// in the store. The store state is unchanged.
    /// </summary>
    SourceNotFound = 5,
}
