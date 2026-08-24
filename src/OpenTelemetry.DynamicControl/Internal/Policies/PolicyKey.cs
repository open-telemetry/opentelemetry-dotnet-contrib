// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Identifies the slot that a telemetry policy occupies, across all configured sources.
/// </summary>
/// <remarks>
/// <para>
/// Cross-source policy identity is the composite of the policy type and the
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

    private readonly string? policyType;
    private readonly string? policyId;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyKey"/> struct.
    /// </summary>
    /// <param name="policyType">The policy type discriminator. Must not be null or whitespace.</param>
    /// <param name="policyId">The provider-assigned policy identifier. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when either component is null, empty, or whitespace.</exception>
    public PolicyKey(string policyType, string policyId)
    {
        Guard.ThrowIfNullOrWhitespace(policyType, nameof(policyType));
        Guard.ThrowIfNullOrWhitespace(policyId, nameof(policyId));

        this.policyType = policyType;
        this.policyId = policyId;
    }

    /// <summary>
    /// Gets the policy type discriminator (e.g. <c>trace-sampling</c>).
    /// </summary>
    public string PolicyType => this.policyType ?? string.Empty;

    /// <summary>
    /// Gets the provider-assigned policy identifier.
    /// </summary>
    public string PolicyId => this.policyId ?? string.Empty;

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
        var typeComparison = string.CompareOrdinal(this.policyType ?? string.Empty, other.policyType ?? string.Empty);
        return typeComparison != 0 ? typeComparison : string.CompareOrdinal(this.policyId ?? string.Empty, other.policyId ?? string.Empty);
    }

    /// <inheritdoc/>
    public bool Equals(PolicyKey other)
        => string.Equals(this.policyType ?? string.Empty, other.policyType ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(this.policyId ?? string.Empty, other.policyId ?? string.Empty, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicyKey other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(this.PolicyType),
            StringComparer.Ordinal.GetHashCode(this.PolicyId));
#else
        unchecked
        {
            var hash = (17 * 31) + StringComparer.Ordinal.GetHashCode(this.PolicyType);
            return (hash * 31) + StringComparer.Ordinal.GetHashCode(this.PolicyId);
        }
#endif
    }

    /// <summary>
    /// Returns a diagnostic representation of the key.
    /// </summary>
    /// <returns>The policy type and identifier, separated by a forward slash.</returns>
    public override string ToString() => this.PolicyType + "/" + this.PolicyId;
}
