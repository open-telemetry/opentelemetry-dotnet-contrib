// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Providers;

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
    /// already accepted or suppressed for this provider. The newer set survives and the revision
    /// is unchanged.
    /// </summary>
    RejectedStaleSequence = 2,

    /// <summary>
    /// The submission's <see cref="PolicyProviderVersion"/> matched the version currently
    /// effective for this provider, so the store state has not changed. The revision is
    /// unchanged, but the provider's maximum sequence has advanced to prevent a later,
    /// lower-sequence submission from replacing the current set.
    /// </summary>
    SuppressedUnchangedVersion = 3,

    /// <summary>
    /// The submission's metadata differed from the metadata pinned at the provider's first
    /// accepted update. The store state is unchanged.
    /// </summary>
    RejectedMetadataMismatch = 4,

    /// <summary>
    /// A <see cref="PolicyStore.RemoveProvider"/> call named a provider that is not currently
    /// in the store. The store state is unchanged.
    /// </summary>
    ProviderNotFound = 5,
}
