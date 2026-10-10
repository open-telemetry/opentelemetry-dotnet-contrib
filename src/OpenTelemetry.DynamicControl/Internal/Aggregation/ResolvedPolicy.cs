// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using OpenTelemetry.DynamicControl.Internal.Policies;
using OpenTelemetry.DynamicControl.Internal.Providers;

namespace OpenTelemetry.DynamicControl.Internal.Aggregation;

/// <summary>
/// The effective policy for one policy key, together with the policies it outranks.
/// </summary>
internal sealed class ResolvedPolicy(
    PolicyKey key,
    TelemetryPolicy policy,
    ProviderRegistrationId provider,
    ImmutableArray<OutrankedPolicy> outrankedPolicies)
{
    /// <summary>
    /// Gets the key this resolution describes.
    /// </summary>
    public PolicyKey Key { get; } = key;

    /// <summary>
    /// Gets the registration identity of the provider that supplied <see cref="Policy"/>.
    /// </summary>
    public ProviderRegistrationId Provider { get; } = provider;

    /// <summary>
    /// Gets the effective policy for <see cref="Key"/>.
    /// </summary>
    /// <remarks>
    /// This is the effective provider's own instance, shared by reference. <see cref="TelemetryPolicy"/>
    /// is immutable, so consumers may hold it for as long as they need it.
    /// </remarks>
    public TelemetryPolicy Policy { get; } = policy;

    /// <summary>
    /// Gets the retained but outranked policies for <see cref="Key"/> that are not effective,
    /// in precedence order, so the first element is the one that becomes effective if the
    /// effective provider stops supplying the key. Empty when only one provider supplied the key.
    /// </summary>
    public ImmutableArray<OutrankedPolicy> OutrankedPolicies { get; } = outrankedPolicies;

    /// <summary>
    /// Returns a diagnostic representation of the resolution.
    /// </summary>
    /// <returns>The policy key and effective provider, separated by a forward slash.</returns>
    public override string ToString() => $"{this.Key}/{this.Provider.Value}";
}
