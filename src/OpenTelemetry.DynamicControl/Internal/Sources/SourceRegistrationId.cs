// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Sources;

/// <summary>
/// Identifies one configured policy source for the lifetime of the process.
/// </summary>
/// <remarks>
/// The policy store retains one snapshot per source, so each configured source needs a
/// stable identity. <see cref="PolicySourceKind"/> alone cannot serve as that identity
/// because an application may configure several sources of the same kind.
/// </remarks>
internal readonly struct SourceRegistrationId : IEquatable<SourceRegistrationId>
{
    /// <summary>
    /// A read-only instance of the <see cref="SourceRegistrationId"/> structure whose values
    /// are all <see langword="default"/>.
    /// </summary>
    public static readonly SourceRegistrationId None;

    private readonly string? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceRegistrationId"/> struct.
    /// </summary>
    /// <param name="value">
    /// The opaque registration value. Must not be null or whitespace, and must be unique
    /// among the configured sources.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public SourceRegistrationId(string value)
    {
        Guard.ThrowIfNullOrWhitespace(value, nameof(value));
        this.value = value;
    }

    /// <summary>
    /// Gets the opaque registration value.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// Determines whether two <see cref="SourceRegistrationId"/> instances identify the same source.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if the identifiers are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(SourceRegistrationId left, SourceRegistrationId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="SourceRegistrationId"/> instances identify different sources.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if the identifiers are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(SourceRegistrationId left, SourceRegistrationId right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(SourceRegistrationId other)
        => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SourceRegistrationId other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Value;
}
