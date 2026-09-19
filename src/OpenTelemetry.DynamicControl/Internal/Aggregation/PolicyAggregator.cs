// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.DynamicControl.Internal.Store;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Aggregation;

/// <summary>
/// Resolves the effective policy for each policy key when multiple policy providers
/// supply the same key.
/// </summary>
/// <remarks>
/// <para>
/// Aggregation is a pure function of the snapshot it is given: it holds no state, has no side
/// effects, and is safe to call concurrently from any thread.
/// </para>
/// <para>
/// Providers take precedence by ascending <see cref="PolicyProviderMetadata.Priority"/>, then by
/// ascending ordinal <see cref="ProviderRegistrationId"/>. Both inputs are fixed by
/// configuration, so the same store contents always resolve the same way, whatever order the
/// providers submitted their sets in.
/// </para>
/// <para>
/// Policies that lose are recorded rather than discarded. The store retains every provider's
/// set, so when a provider retracts a key, re-aggregating promotes the next provider in
/// precedence order without that provider resubmitting.
/// </para>
/// </remarks>
internal static class PolicyAggregator
{
    /// <summary>
    /// Resolves the effective policy for every key supplied by any policy provider in
    /// <paramref name="storeSnapshot"/>.
    /// </summary>
    /// <param name="storeSnapshot">The store state to resolve.</param>
    /// <returns>The resolution of <paramref name="storeSnapshot"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="storeSnapshot"/> is null.</exception>
    public static ResolvedPolicySnapshot Aggregate(PolicyStoreSnapshot storeSnapshot)
    {
        Guard.ThrowIfNull(storeSnapshot);

        var revision = storeSnapshot.Revision;

        if (storeSnapshot.Providers.IsEmpty)
        {
            return revision == 0
                ? ResolvedPolicySnapshot.Empty
                : new ResolvedPolicySnapshot(revision, []);
        }

        var byPrecedence = storeSnapshot.Providers.Sort(ComparePrecedence);

        var winners = new Dictionary<PolicyKey, (TelemetryPolicy Policy, PolicyProviderMetadata Provider)>();
        Dictionary<PolicyKey, List<OutrankedPolicy>>? outrankedByKey = null;

        foreach (var providerSnapshot in byPrecedence)
        {
            var provider = providerSnapshot.Metadata;

            foreach (var policy in providerSnapshot.Policies)
            {
                var key = PolicyKey.FromPolicy(policy);

                if (!winners.TryGetValue(key, out var winner))
                {
                    winners.Add(key, (policy, provider));
                    continue;
                }

                var reason = provider.Priority == winner.Provider.Priority
                    ? PolicyAggregationReason.Conflicting
                    : PolicyAggregationReason.Superseded;

                outrankedByKey ??= [];

                if (!outrankedByKey.TryGetValue(key, out var outranked))
                {
                    outranked = [];
                    outrankedByKey.Add(key, outranked);
                }

                outranked.Add(new OutrankedPolicy(provider.RegistrationId, policy, reason));
            }
        }

        var resolved = new ResolvedPolicy[winners.Count];
        var next = 0;

        foreach (var winner in winners)
        {
            ImmutableArray<OutrankedPolicy> outranked =
                outrankedByKey is not null && outrankedByKey.TryGetValue(winner.Key, out var losers)
                    ? [.. losers]
                    : [];

            resolved[next++] = new ResolvedPolicy(
                winner.Key,
                winner.Value.Policy,
                winner.Value.Provider.RegistrationId,
                outranked);
        }

        return new ResolvedPolicySnapshot(revision, resolved);
    }

    private static int ComparePrecedence(PolicyProviderSnapshot x, PolicyProviderSnapshot y)
    {
        var priorityComparison = x.Metadata.Priority.CompareTo(y.Metadata.Priority);

        return priorityComparison != 0
            ? priorityComparison
            : string.CompareOrdinal(x.RegistrationId.Value, y.RegistrationId.Value);
    }
}
