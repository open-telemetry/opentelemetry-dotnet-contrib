// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Store;

/// <summary>
/// One policy provider's complete, validated, immutable policy set at a single point in time.
/// </summary>
/// <remarks>
/// Once published by <see cref="PolicyStore"/>, this instance is read-only.
/// Concurrent reads of a published snapshot are safe without any additional locking.
/// </remarks>
internal sealed class PolicyProviderSnapshot
{
    private readonly Dictionary<PolicyKey, TelemetryPolicy> lookup;

    private PolicyProviderSnapshot(
        PolicyProviderMetadata metadata,
        long sequence,
        PolicyProviderVersion version,
        TelemetryPolicy[] policies,
        Dictionary<PolicyKey, TelemetryPolicy> lookup)
    {
        this.Metadata = metadata;
        this.Sequence = sequence;
        this.Version = version;
        this.Policies = ImmutableCollectionsMarshal.AsImmutableArray(policies);
        this.lookup = new Dictionary<PolicyKey, TelemetryPolicy>(lookup);
    }

    /// <summary>
    /// Gets the metadata that fully describes the provider that produced this snapshot.
    /// </summary>
    public PolicyProviderMetadata Metadata { get; }

    /// <summary>
    /// Gets the registration identity of the provider. Equivalent to <see cref="PolicyProviderMetadata.RegistrationId"/>.
    /// </summary>
    public ProviderRegistrationId RegistrationId => this.Metadata.RegistrationId;

    /// <summary>
    /// Gets the monotonically increasing, caller-assigned stamp that orders submissions
    /// from this provider.
    /// </summary>
    /// <remarks>
    /// This is the sequence number of the submission that produced this effective set.
    /// It may be lower than the highest sequence the store has seen for this provider, because
    /// a later submission may have been suppressed (version unchanged) rather than applied.
    /// The store's maximum sequence seen for this provider, which governs staleness, is maintained
    /// separately and is not available from the snapshot.
    /// </remarks>
    public long Sequence { get; }

    /// <summary>
    /// Gets the change-detection token carried by this snapshot.
    /// </summary>
    public PolicyProviderVersion Version { get; }

    /// <summary>
    /// Gets the policies in this snapshot, sorted by <see cref="PolicyKeyComparer"/> order.
    /// </summary>
    public ImmutableArray<TelemetryPolicy> Policies { get; }

    /// <summary>
    /// Gets a value indicating whether this snapshot contains no policies.
    /// </summary>
    /// <remarks>
    /// A valid empty snapshot is a retraction: the provider is live and intentionally
    /// supplying nothing. This is distinct from the provider not having submitted at all
    /// (absent from the store) or having been removed (also absent, but a configuration
    /// lifecycle event).
    /// </remarks>
    public bool IsEmpty => this.Policies.IsEmpty;

    /// <summary>
    /// Attempts to create a new <see cref="PolicyProviderSnapshot"/> from the supplied inputs.
    /// </summary>
    /// <param name="metadata">
    /// The metadata of the provider. Must not be <see langword="default"/>.
    /// </param>
    /// <param name="sequence">The caller-assigned submission stamp. Must be greater than or equal to 1.</param>
    /// <param name="version">The change-detection token for this submission.</param>
    /// <param name="policies">
    /// The complete, validated policy set for this provider.
    /// </param>
    /// <param name="snapshot">
    /// When this method returns <see langword="true"/>, the new snapshot; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="error">
    /// When this method returns <see langword="false"/>, a description of the validation
    /// failure suitable for reporting back to the transport or coordinator. When
    /// <see langword="true"/>, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the snapshot was created successfully; <see langword="false"/>
    /// if validation failed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="policies"/> is null.</exception>
    public static bool TryCreate(
        PolicyProviderMetadata metadata,
        long sequence,
        PolicyProviderVersion version,
        IReadOnlyList<TelemetryPolicy?> policies,
        [NotNullWhen(true)] out PolicyProviderSnapshot? snapshot,
        [NotNullWhen(false)] out string? error)
    {
        Guard.ThrowIfNull(policies);

        if (metadata.Equals(default))
        {
            snapshot = null;
            error = "The metadata must not be a default PolicyProviderMetadata instance.";
            return false;
        }

        if (sequence < 1)
        {
            snapshot = null;
            error = $"The sequence must be greater than or equal to 1 (was {sequence}).";
            return false;
        }

        var count = policies.Count;
        var keys = new PolicyKey[count];
        var policyItems = new TelemetryPolicy[count];
        var policyMap = new Dictionary<PolicyKey, TelemetryPolicy>(count);

        for (var i = 0; i < count; i++)
        {
            var policy = policies[i];

            if (policy is null)
            {
                snapshot = null;
                error = $"The policies list must not contain null elements (null at index {i}).";
                return false;
            }

            // An empty Id is unrepresentable: the TelemetryPolicy constructor rejects a
            // default PolicyId, and a non-default PolicyId cannot be blank. PolicyType is
            // abstract, so a derived type could still return an empty value.
            if (policy.PolicyType.IsEmpty)
            {
                snapshot = null;
                error = $"The policy at index {i} has an empty PolicyType.";
                return false;
            }

            var key = new PolicyKey(policy.PolicyType, policy.Id);
#if NET || NETSTANDARD2_1_OR_GREATER
            var added = policyMap.TryAdd(key, policy);
#else
            var added = !policyMap.ContainsKey(key);
            if (added)
            {
                policyMap[key] = policy;
            }

#endif
            if (!added)
            {
                snapshot = null;
                error = $"Duplicate policy key: {key}. A policy snapshot must not contain two policies with the same PolicyType and Id.";
                return false;
            }

            keys[i] = key;
            policyItems[i] = policy;
        }

        Array.Sort(keys, policyItems, PolicyKeyComparer.Default);

        snapshot = new PolicyProviderSnapshot(metadata, sequence, version, policyItems, policyMap);
        error = null;
        return true;
    }

    /// <summary>
    /// Looks up the policy at the given key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="policy">
    /// When this method returns <see langword="true"/>, the policy; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the key was found; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetPolicy(PolicyKey key, [NotNullWhen(true)] out TelemetryPolicy? policy)
        => this.lookup.TryGetValue(key, out policy);
}
