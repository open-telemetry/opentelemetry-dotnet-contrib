// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Store;

namespace OpenTelemetry.DynamicControl.Internal.Aggregation;

/// <summary>
/// The complete, immutable set of effective policies resolved from one store revision.
/// </summary>
/// <remarks>
/// <para>
/// A key absent from <see cref="Policies"/> has no applicable policy from any provider.
/// No fallback policy is synthesized; each policy implementer must explicitly handle
/// absence according to that policy type's contract.
/// </para>
/// </remarks>
internal sealed class ResolvedPolicySnapshot
{
    /// <summary>
    /// The resolution of an empty store, at revision 0.
    /// </summary>
    public static readonly ResolvedPolicySnapshot Empty = new(0, []);

    // Not using FrozenDictionary here. Each snapshot receives few update-time lookups, so freezing would
    // likely not amortize its higher construction cost.
    private readonly Dictionary<PolicyKey, ResolvedPolicy> lookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolvedPolicySnapshot"/> class.
    /// </summary>
    /// <param name="storeRevision">The revision of the store snapshot that was resolved.</param>
    /// <param name="policies">
    /// The resolved policies, at most one per key. Sorted in place and then owned by the new
    /// instance.
    /// </param>
    internal ResolvedPolicySnapshot(long storeRevision, ResolvedPolicy[] policies)
    {
        this.StoreRevision = storeRevision;

        var count = policies.Length;
        if (count == 0)
        {
            this.Policies = [];
            this.lookup = [];
            return;
        }

        var keys = new PolicyKey[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = policies[i].Key;
        }

        Array.Sort(keys, policies, PolicyKeyComparer.Default);

        var lookupDictionary = new Dictionary<PolicyKey, ResolvedPolicy>(count);
        foreach (var policy in policies)
        {
            lookupDictionary[policy.Key] = policy;
        }

        this.Policies = ImmutableCollectionsMarshal.AsImmutableArray(policies);
        this.lookup = lookupDictionary;
    }

    /// <summary>
    /// Gets the revision of the store snapshot this resolution describes.
    /// </summary>
    /// <remarks>
    /// Correlates a resolution with the <see cref="PolicyStoreSnapshot.Revision"/> it was
    /// produced from.
    /// </remarks>
    public long StoreRevision { get; }

    /// <summary>
    /// Gets the effective policies, sorted by <see cref="PolicyKeyComparer"/> order.
    /// </summary>
    public ImmutableArray<ResolvedPolicy> Policies { get; }

    /// <summary>
    /// Gets a value indicating whether no policy is effective at any key.
    /// </summary>
    public bool IsEmpty => this.Policies.IsEmpty;

    /// <summary>
    /// Looks up the resolution for the given key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="resolved">
    /// When this method returns <see langword="true"/>, the resolution; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a policy is effective at <paramref name="key"/>; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public bool TryGetPolicy(PolicyKey key, [NotNullWhen(true)] out ResolvedPolicy? resolved)
        => this.lookup.TryGetValue(key, out resolved);
}
