// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OpenTelemetry.DynamicControl.Internal.Sources;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// The complete, immutable state of the policy store at one revision.
/// </summary>
/// <remarks>
/// <para>
/// Every accepted change to the store produces a new <see cref="PolicyStoreSnapshot"/>
/// instance. Unchanged <see cref="PolicySourceSnapshot"/> instances are reused by reference,
/// so consumers can use reference identity to detect whether a particular source changed.
/// </para>
/// <para>
/// Neither this type nor <see cref="PolicySourceSnapshot"/> overrides
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

    private readonly Dictionary<SourceRegistrationId, PolicySourceSnapshot> lookup;

    internal PolicyStoreSnapshot(long revision, Dictionary<SourceRegistrationId, PolicySourceSnapshot> sources)
    {
        this.Revision = revision;

        var count = sources.Count;
        if (count == 0)
        {
            this.Sources = [];
            this.lookup = [];
            return;
        }

        var ids = new string[count];
        var snapshots = new PolicySourceSnapshot[count];
        var i = 0;
        foreach (var kvp in sources)
        {
            ids[i] = kvp.Key.Value;
            snapshots[i] = kvp.Value;
            i++;
        }

        Array.Sort(ids, snapshots, StringComparer.Ordinal);

        this.Sources = ImmutableCollectionsMarshal.AsImmutableArray(snapshots);

        var lookupDictionary = new Dictionary<SourceRegistrationId, PolicySourceSnapshot>(count);
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
    /// <see cref="PolicyStore.ReplaceSource"/> or <see cref="PolicyStore.RemoveSource"/>).
    /// Rejected and suppressed submissions never move it, and it never resets, even when
    /// all sources are removed.
    /// </para>
    /// </remarks>
    public long Revision { get; }

    /// <summary>
    /// Gets the current source snapshots, sorted by ordinal <see cref="SourceRegistrationId.Value"/>.
    /// </summary>
    /// <remarks>
    /// Ordering is by identity, not by <see cref="PolicySourceMetadata.Priority"/>. Priority
    /// ordering is an aggregation concern; identity ordering is neutral and stable,
    /// giving any priority-aware consumer a deterministic baseline to sort from.
    /// </remarks>
    public ImmutableArray<PolicySourceSnapshot> Sources { get; }

    /// <summary>
    /// Looks up the snapshot for the given source.
    /// </summary>
    /// <param name="registrationId">The registration identity to look up.</param>
    /// <param name="source">
    /// When this method returns <see langword="true"/>, the snapshot; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the source is present; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetSource(
        SourceRegistrationId registrationId,
        [NotNullWhen(true)] out PolicySourceSnapshot? source)
        => this.lookup.TryGetValue(registrationId, out source);
}
