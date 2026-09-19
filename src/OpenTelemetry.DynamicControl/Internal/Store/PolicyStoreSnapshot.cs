// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// The complete, immutable state of the policy store at one revision.
/// </summary>
/// <remarks>
/// <para>
/// Every accepted change to the store produces a new <see cref="PolicyStoreSnapshot"/>
/// instance. Unchanged <see cref="PolicyProviderSnapshot"/> instances are reused by reference,
/// so consumers can use reference identity to detect whether a particular provider changed.
/// </para>
/// <para>
/// Neither this type nor <see cref="PolicyProviderSnapshot"/> overrides
/// <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode()"/>: reference
/// identity is the meaningful comparison for published snapshots.
/// </para>
/// </remarks>
internal sealed class PolicyStoreSnapshot
{
    /// <summary>
    /// The empty store snapshot at revision 0. Every <see cref="PolicyStore"/> starts
    /// from this shared instance.
    /// </summary>
    public static readonly PolicyStoreSnapshot Empty = new(0, []);

    private readonly Dictionary<ProviderRegistrationId, PolicyProviderSnapshot> lookup;

    internal PolicyStoreSnapshot(long revision, Dictionary<ProviderRegistrationId, PolicyProviderSnapshot> providers)
    {
        this.Revision = revision;

        var count = providers.Count;
        if (count == 0)
        {
            this.Providers = [];
            this.lookup = [];
            return;
        }

        var ids = new string[count];
        var snapshots = new PolicyProviderSnapshot[count];
        var i = 0;
        foreach (var kvp in providers)
        {
            ids[i] = kvp.Key.Value;
            snapshots[i] = kvp.Value;
            i++;
        }

        Array.Sort(ids, snapshots, StringComparer.Ordinal);

        this.Providers = ImmutableCollectionsMarshal.AsImmutableArray(snapshots);

        var lookupDictionary = new Dictionary<ProviderRegistrationId, PolicyProviderSnapshot>(count);
        foreach (var snapshot in snapshots)
        {
            lookupDictionary[snapshot.RegistrationId] = snapshot;
        }

        this.lookup = lookupDictionary;
    }

    /// <summary>
    /// Gets the monotonically increasing revision counter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Starts at <c>0</c> for <see cref="Empty"/> and increments by exactly one for each
    /// accepted change (a <see cref="PolicyStoreUpdateStatus.Applied"/> result from either
    /// <see cref="PolicyStore.ReplaceProvider"/> or <see cref="PolicyStore.RemoveProvider"/>).
    /// Rejected and suppressed submissions never move it, and it never resets, even when
    /// all providers are removed.
    /// </para>
    /// </remarks>
    public long Revision { get; }

    /// <summary>
    /// Gets the current provider snapshots, sorted by ordinal <see cref="ProviderRegistrationId.Value"/>.
    /// </summary>
    /// <remarks>
    /// Ordering is by identity, not by <see cref="PolicyProviderMetadata.Priority"/>. Priority
    /// ordering is an aggregation concern; identity ordering is neutral and stable,
    /// giving any priority-aware consumer a deterministic baseline to sort from.
    /// </remarks>
    public ImmutableArray<PolicyProviderSnapshot> Providers { get; }

    /// <summary>
    /// Looks up the snapshot for the given provider.
    /// </summary>
    /// <param name="registrationId">The registration identity to look up.</param>
    /// <param name="provider">
    /// When this method returns <see langword="true"/>, the snapshot; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the provider is present; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetProvider(
        ProviderRegistrationId registrationId,
        [NotNullWhen(true)] out PolicyProviderSnapshot? provider)
        => this.lookup.TryGetValue(registrationId, out provider);
}
