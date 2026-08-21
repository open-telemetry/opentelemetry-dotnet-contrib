// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// One item retained in a window's kept sample: the token that identified it
/// while it was buffered, and the callsite it belongs to.
/// </summary>
/// <typeparam name="TCallsite">The callsite key type.</typeparam>
public readonly struct KeptItem<TCallsite> : IEquatable<KeptItem<TCallsite>>
    where TCallsite : notnull
{
    internal KeptItem(long token, TCallsite callsite)
    {
        this.Token = token;
        this.Callsite = callsite;
    }

    /// <summary>
    /// Gets the token this item was admitted with, matching the
    /// <see cref="OfferOutcome.Token"/> returned when it entered the reservoir.
    /// </summary>
    public long Token { get; }

    /// <summary>Gets the callsite this item belongs to.</summary>
    public TCallsite Callsite { get; }

    /// <summary>Determines whether two items are equal.</summary>
    /// <param name="left">The left item.</param>
    /// <param name="right">The right item.</param>
    /// <returns><see langword="true"/> if the items are equal.</returns>
    public static bool operator ==(KeptItem<TCallsite> left, KeptItem<TCallsite> right) => left.Equals(right);

    /// <summary>Determines whether two items are unequal.</summary>
    /// <param name="left">The left item.</param>
    /// <param name="right">The right item.</param>
    /// <returns><see langword="true"/> if the items are unequal.</returns>
    public static bool operator !=(KeptItem<TCallsite> left, KeptItem<TCallsite> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(KeptItem<TCallsite> other) =>
        this.Token == other.Token &&
        EqualityComparer<TCallsite>.Default.Equals(this.Callsite, other.Callsite);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeptItem<TCallsite> other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Token, this.Callsite);
}
