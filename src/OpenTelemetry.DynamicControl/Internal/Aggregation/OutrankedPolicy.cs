// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Internal.Aggregation;

/// <summary>
/// One retained policy that is not effective for its key, and the reason it is not.
/// </summary>
/// <remarks>
/// Outranked policies are retained rather than discarded, so a provider can be told that its
/// policy was received and kept but is not in force.
/// </remarks>
internal readonly struct OutrankedPolicy
{
    internal OutrankedPolicy(
        ProviderRegistrationId provider,
        TelemetryPolicy policy,
        PolicyAggregationReason reason)
    {
        this.Provider = provider;
        this.Policy = policy;
        this.Reason = reason;
    }

    /// <summary>
    /// Gets the registration identity of the provider that supplied this policy.
    /// </summary>
    public ProviderRegistrationId Provider { get; }

    /// <summary>
    /// Gets the policy instance supplied by <see cref="Provider"/>, shared by reference.
    /// </summary>
    public TelemetryPolicy Policy { get; }

    /// <summary>
    /// Gets the reason this policy is not effective for its key.
    /// </summary>
    public PolicyAggregationReason Reason { get; }

    /// <summary>
    /// Returns a diagnostic representation of the outranked policy.
    /// </summary>
    /// <returns>The policy key, supplying provider, and reason, separated by forward slashes.</returns>
    public override string ToString()
        => $"{PolicyKey.FromPolicy(this.Policy)}/{this.Provider.Value}/{this.Reason}";
}
