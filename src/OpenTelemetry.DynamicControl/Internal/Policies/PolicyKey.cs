// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Identifies the slot that a telemetry policy occupies, across all configured providers.
/// </summary>
/// <remarks>
/// <para>
/// Cross-provider policy identity is the composite of the policy type and the
/// provider-assigned policy identifier, rather than the identifier alone.
/// </para>
/// </remarks>
internal readonly struct PolicyKey : IEquatable<PolicyKey>, IComparable<PolicyKey>
{
    /// <summary>
    /// A read-only instance of the <see cref="PolicyKey"/> structure whose field values
    /// are all <see langword="default"/>.
    /// </summary>
    public static readonly PolicyKey Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyKey"/> struct.
    /// </summary>
    /// <param name="policyType">The policy type discriminator. Must not be <see cref="PolicyType.Empty"/>.</param>
    /// <param name="policyId">The provider-assigned policy identifier. Must not be <see cref="PolicyId.Empty"/>.</param>
    /// <exception cref="ArgumentException">Thrown when either component is its default value.</exception>
    public PolicyKey(PolicyType policyType, PolicyId policyId)
    {
        Guard.ThrowIfDefault(policyType, nameof(policyType));
        Guard.ThrowIfDefault(policyId, nameof(policyId));

        this.PolicyType = policyType;
        this.PolicyId = policyId;
    }

    /// <summary>
    /// Gets the policy type discriminator (e.g. <c>trace-sampling</c>).
    /// </summary>
    public PolicyType PolicyType { get; }

    /// <summary>
    /// Gets the provider-assigned policy identifier.
    /// </summary>
    public PolicyId PolicyId { get; }

    /// <summary>
    /// Determines whether two <see cref="PolicyKey"/> instances identify the same policy.
    /// </summary>
    /// <param name="left">The first key to compare.</param>
    /// <param name="right">The second key to compare.</param>
    /// <returns><see langword="true"/> if the keys are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PolicyKey left, PolicyKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PolicyKey"/> instances identify different policies.
    /// </summary>
    /// <param name="left">The first key to compare.</param>
    /// <param name="right">The second key to compare.</param>
    /// <returns><see langword="true"/> if the keys are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PolicyKey left, PolicyKey right) => !left.Equals(right);

    /// <summary>
    /// Creates the key identifying the slot that <paramref name="policy"/> occupies.
    /// </summary>
    /// <param name="policy">The policy to derive a key from.</param>
    /// <returns>The key composed of the policy's type and identifier.</returns>
    /// <remarks>
    /// The returned key depends only on identity, never on policy content, so an updated
    /// value for the same policy resolves to the same slot.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the policy reports a blank type.</exception>
    public static PolicyKey FromPolicy(TelemetryPolicy policy)
    {
        Guard.ThrowIfNull(policy);

        return new PolicyKey(policy.PolicyType, policy.Id);
    }

    /// <inheritdoc/>
    public int CompareTo(PolicyKey other)
    {
        var typeComparison = this.PolicyType.CompareTo(other.PolicyType);
        return typeComparison != 0 ? typeComparison : this.PolicyId.CompareTo(other.PolicyId);
    }

    /// <inheritdoc/>
    public bool Equals(PolicyKey other)
        => this.PolicyType.Equals(other.PolicyType)
            && this.PolicyId.Equals(other.PolicyId);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicyKey other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(
            this.PolicyType,
            this.PolicyId);
#else
        unchecked
        {
            var hash = (17 * 31) + this.PolicyType.GetHashCode();
            return (hash * 31) + this.PolicyId.GetHashCode();
        }
#endif
    }

    /// <summary>
    /// Returns a diagnostic representation of the key.
    /// </summary>
    /// <returns>The policy type and identifier, separated by a forward slash.</returns>
    public override string ToString() => this.PolicyType.Value + "/" + this.PolicyId.Value;
}
