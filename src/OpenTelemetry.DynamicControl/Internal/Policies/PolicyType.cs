// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Identifies the kind of telemetry policy.
/// </summary>
internal readonly struct PolicyType : IEquatable<PolicyType>, IComparable<PolicyType>
{
    /// <summary>
    /// A read-only instance whose value is <see cref="string.Empty"/>.
    /// </summary>
    public static readonly PolicyType Empty;

    private readonly string? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyType"/> struct.
    /// </summary>
    /// <param name="value">The policy type discriminator. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public PolicyType(string value)
    {
        Guard.ThrowIfNullOrWhitespace(value, nameof(value));
        this.value = value;
    }

    /// <summary>
    /// Gets the policy type discriminator.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether this instance is <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this.value is null;

    /// <summary>
    /// Determines whether two <see cref="PolicyType"/> instances are equal.
    /// </summary>
    /// <param name="left">The first policy type to compare.</param>
    /// <param name="right">The second policy type to compare.</param>
    /// <returns><see langword="true"/> when the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PolicyType left, PolicyType right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PolicyType"/> instances are different.
    /// </summary>
    /// <param name="left">The first policy type to compare.</param>
    /// <param name="right">The second policy type to compare.</param>
    /// <returns><see langword="true"/> when the values are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PolicyType left, PolicyType right) => !left.Equals(right);

    /// <inheritdoc/>
    public int CompareTo(PolicyType other) => string.CompareOrdinal(this.Value, other.Value);

    /// <inheritdoc/>
    public bool Equals(PolicyType other)
        => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicyType other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Value;
}
