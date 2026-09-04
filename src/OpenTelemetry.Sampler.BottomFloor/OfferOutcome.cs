// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// The result of offering one arrival to <see cref="BottomFloorSampler{TCallsite}.Offer"/>.
/// <para/>
/// A bottom-k reservoir may admit an arrival now and evict it later, so a caller
/// that buffers admitted events should key its buffer by <see cref="Token"/>,
/// add on admission, and remove on eviction. The set of tokens still buffered at
/// <see cref="BottomFloorSampler{TCallsite}.CloseWindow"/> is the window's kept sample.
/// </summary>
internal readonly struct OfferOutcome
{
    private OfferOutcome(bool admitted, long token, bool evicted, long evictedToken)
    {
        this.Admitted = admitted;
        this.Token = token;
        this.Evicted = evicted;
        this.EvictedToken = evictedToken;
    }

    /// <summary>
    /// Gets a value indicating whether the arrival entered the reservoir. When
    /// <see langword="false"/> the arrival was dropped and the other members carry no meaning.
    /// </summary>
    public bool Admitted { get; }

    /// <summary>
    /// Gets the token identifying the admitted arrival. Meaningful only when
    /// <see cref="Admitted"/> is <see langword="true"/>.
    /// </summary>
    public long Token { get; }

    /// <summary>
    /// Gets a value indicating whether an earlier admitted arrival was evicted to
    /// make room for this one.
    /// </summary>
    public bool Evicted { get; }

    /// <summary>
    /// Gets the token of the evicted arrival. Meaningful only when
    /// <see cref="Evicted"/> is <see langword="true"/>.
    /// </summary>
    public long EvictedToken { get; }

    internal static OfferOutcome Admit(long token) => new(true, token, false, 0L);

    internal static OfferOutcome AdmitEvicting(long token, long evictedToken) => new(true, token, true, evictedToken);
}
