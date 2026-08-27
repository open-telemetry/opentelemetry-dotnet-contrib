// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Policies;

/// <summary>
/// Identifies one telemetry policy within its policy type.
/// </summary>
internal readonly struct PolicyId : IEquatable<PolicyId>, IComparable<PolicyId>
{
    /// <summary>
    /// A read-only instance whose value is <see cref="string.Empty"/>.
    /// </summary>
    public static readonly PolicyId Empty;

    private readonly string? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyId"/> struct.
    /// </summary>
    /// <param name="value">The provider-assigned policy identifier. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public PolicyId(string value)
    {
        Guard.ThrowIfNullOrWhitespace(value, nameof(value));
        this.value = value;
    }

    /// <summary>
    /// Gets the provider-assigned policy identifier.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether this instance is <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this.value is null;

    /// <summary>
    /// Determines whether two <see cref="PolicyId"/> instances are equal.
    /// </summary>
    /// <param name="left">The first policy identifier to compare.</param>
    /// <param name="right">The second policy identifier to compare.</param>
    /// <returns><see langword="true"/> when the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PolicyId left, PolicyId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PolicyId"/> instances are different.
    /// </summary>
    /// <param name="left">The first policy identifier to compare.</param>
    /// <param name="right">The second policy identifier to compare.</param>
    /// <returns><see langword="true"/> when the values are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PolicyId left, PolicyId right) => !left.Equals(right);

    /// <inheritdoc/>
    public int CompareTo(PolicyId other) => string.CompareOrdinal(this.Value, other.Value);

    /// <inheritdoc/>
    public bool Equals(PolicyId other)
        => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicyId other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Value;
}
