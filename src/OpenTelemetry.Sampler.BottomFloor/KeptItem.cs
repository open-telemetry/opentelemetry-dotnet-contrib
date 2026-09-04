// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// One item retained in a window's kept sample: the token that identified it
/// while it was buffered, and the callsite it belongs to.
/// </summary>
/// <typeparam name="TCallsite">The callsite key type.</typeparam>
internal readonly struct KeptItem<TCallsite>
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
}
