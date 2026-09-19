// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.DynamicControl.Internal.Providers;

/// <summary>
/// Identifies where in a policy payload a particular entry appeared.
/// </summary>
/// <remarks>
/// An entry is identified either by the key it declared or, when it declared no key that
/// could be read, by its position in the payload.
/// </remarks>
internal readonly struct PayloadEntryLocation : IEquatable<PayloadEntryLocation>
{
    /// <summary>
    /// A location that identifies no entry.
    /// </summary>
    public static readonly PayloadEntryLocation Empty;

    private readonly string? key;

    private readonly int index;

    // Held separately rather than encoded into the index, so that a default instance
    // identifies no location while every int, including int.MaxValue, remains a usable
    // position.
    private readonly bool hasIndex;

    private PayloadEntryLocation(string? key, int index, bool hasIndex)
    {
        this.key = key;
        this.index = index;
        this.hasIndex = hasIndex;
    }

    /// <summary>
    /// Determines whether two locations identify the same entry in the same way.
    /// </summary>
    /// <param name="left">The first location to compare.</param>
    /// <param name="right">The second location to compare.</param>
    /// <returns><see langword="true"/> if the locations are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(PayloadEntryLocation left, PayloadEntryLocation right) => left.Equals(right);

    /// <summary>
    /// Determines whether two locations identify different entries.
    /// </summary>
    /// <param name="left">The first location to compare.</param>
    /// <param name="right">The second location to compare.</param>
    /// <returns><see langword="true"/> if the locations are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(PayloadEntryLocation left, PayloadEntryLocation right) => !left.Equals(right);

    /// <summary>
    /// Creates a location identifying the entry that declared a given key.
    /// </summary>
    /// <param name="key">The key the entry declared. Must not be null or whitespace.</param>
    /// <returns>A location identified by key.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is null, empty, or whitespace.
    /// </exception>
    public static PayloadEntryLocation ForKey(string key)
    {
        Guard.ThrowIfNullOrWhitespace(key);

        return new(key, 0, false);
    }

    /// <summary>
    /// Creates a location identifying an entry by its position in the payload.
    /// </summary>
    /// <param name="index">The zero-based position of the entry. Must be non-negative.</param>
    /// <returns>A location identified by position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative.
    /// </exception>
    public static PayloadEntryLocation ForIndex(int index)
    {
        Guard.ThrowIfNegative(index);

        return new(null, index, true);
    }

    /// <summary>
    /// Gets the key the entry declared, when it is identified by key.
    /// </summary>
    /// <param name="key">
    /// When this method returns <see langword="true"/>, the key the entry declared;
    /// otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if this location is identified by key; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public bool TryGetKey([NotNullWhen(true)] out string? key)
    {
        key = this.key;
        return key is not null;
    }

    /// <summary>
    /// Gets the position of the entry, when it is identified by position.
    /// </summary>
    /// <param name="index">
    /// When this method returns <see langword="true"/>, the zero-based position of the
    /// entry; otherwise zero.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if this location is identified by position; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public bool TryGetIndex(out int index)
    {
        index = this.hasIndex ? this.index : 0;
        return this.hasIndex;
    }

    /// <inheritdoc/>
    public bool Equals(PayloadEntryLocation other)
        => this.hasIndex == other.hasIndex
            && this.index == other.index
            && string.Equals(this.key, other.key, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PayloadEntryLocation other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
#if NET || NETSTANDARD2_1_OR_GREATER
        => HashCode.Combine(
            this.key is null ? 0 : StringComparer.Ordinal.GetHashCode(this.key),
            this.index,
            this.hasIndex);
#else
    {
        unchecked
        {
            var hash = (17 * 31) + (this.key is null ? 0 : StringComparer.Ordinal.GetHashCode(this.key));
            hash = (hash * 31) + this.index;
            return (hash * 31) + (this.hasIndex ? 1 : 0);
        }
    }
#endif

    /// <summary>
    /// Returns a diagnostic representation of the location.
    /// </summary>
    /// <returns>
    /// The key, the position in square brackets, or <c>(none)</c> when the location
    /// identifies neither.
    /// </returns>
    public override string ToString()
        => this.key
            ?? (this.hasIndex
                ? string.Format(CultureInfo.InvariantCulture, "[{0}]", this.index)
                : "(none)");
}
