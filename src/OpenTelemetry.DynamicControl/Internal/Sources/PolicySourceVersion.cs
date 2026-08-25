// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// A change-detection token supplied by a policy source and compared only for equality.
/// </summary>
/// <remarks>
/// <para>
/// The token is intentionally unordered: an OpAMP config hash, an HTTP ETag, a file
/// content hash, and a numeric config version are all valid representations, and none of
/// them is globally comparable. The type therefore does not implement
/// <see cref="IComparable{T}"/>. Any comparison beyond equality is a misuse.
/// </para>
/// <para>
/// Equality is ordinal and case-sensitive: tokens that differ only in case are different
/// tokens.
/// </para>
/// </remarks>
internal readonly struct PolicySourceVersion : IEquatable<PolicySourceVersion>
{
    /// <summary>
    /// A read-only instance of the <see cref="PolicySourceVersion"/> structure whose field
    /// values are all <see langword="default"/>.
    /// </summary>
    /// <remarks>
    /// When the effective version of a snapshot is <see cref="Empty"/>, suppression is
    /// not possible: every submission from that source is applied regardless of content.
    /// </remarks>
    public static readonly PolicySourceVersion Empty;

    private readonly string? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicySourceVersion"/> struct.
    /// </summary>
    /// <param name="value">The version token. Must not be null, empty, or whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is null, empty, or whitespace.
    /// </exception>
    public PolicySourceVersion(string value)
    {
        Guard.ThrowIfNullOrWhitespace(value, nameof(value));
        this.value = value;
    }

    /// <summary>
    /// Gets the raw token value, or <see cref="string.Empty"/> when this instance is
    /// <see cref="Empty"/>.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether this instance is <see cref="Empty"/>.
    /// </summary>
    public bool IsEmpty => this.value is null;

    /// <summary>
    /// Determines whether two <see cref="PolicySourceVersion"/> instances represent the same token.
    /// </summary>
    /// <param name="left">The first version to compare.</param>
    /// <param name="right">The second version to compare.</param>
    /// <returns><see langword="true"/> if equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PolicySourceVersion left, PolicySourceVersion right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="PolicySourceVersion"/> instances represent different tokens.
    /// </summary>
    /// <param name="left">The first version to compare.</param>
    /// <param name="right">The second version to compare.</param>
    /// <returns><see langword="true"/> if not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PolicySourceVersion left, PolicySourceVersion right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(PolicySourceVersion other)
        => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PolicySourceVersion other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Value;
}
