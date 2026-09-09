// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Identifies one configured policy provider for the lifetime of the process.
/// </summary>
/// <remarks>
/// The policy store retains one snapshot per provider, so each configured provider needs a
/// stable identity. <see cref="PolicyProviderKind"/> alone cannot serve as that identity
/// because an application may configure several providers of the same kind.
/// </remarks>
internal readonly struct ProviderRegistrationId : IEquatable<ProviderRegistrationId>
{
    /// <summary>
    /// A read-only instance of the <see cref="ProviderRegistrationId"/> structure whose field
    /// values are all <see langword="default"/>.
    /// </summary>
    public static readonly ProviderRegistrationId Empty;

    private readonly string? value;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderRegistrationId"/> struct.
    /// </summary>
    /// <param name="value">
    /// The registration value. Must not be null or whitespace, and must be unique
    /// among the configured providers.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public ProviderRegistrationId(string value)
    {
        Guard.ThrowIfNullOrWhitespace(value, nameof(value));
        this.value = value;
    }

    /// <summary>
    /// Gets the registration value.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// Determines whether two <see cref="ProviderRegistrationId"/> instances identify the same provider.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if the identifiers are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ProviderRegistrationId left, ProviderRegistrationId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="ProviderRegistrationId"/> instances identify different providers.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if the identifiers are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ProviderRegistrationId left, ProviderRegistrationId right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ProviderRegistrationId other)
        => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProviderRegistrationId other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Value;
}
