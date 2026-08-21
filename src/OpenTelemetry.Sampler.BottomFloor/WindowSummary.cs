// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// The outcome of one closed window: the threshold that cleared it, the items
/// kept, and the per-callsite estimates.
/// </summary>
/// <typeparam name="TCallsite">The callsite key type.</typeparam>
public sealed class WindowSummary<TCallsite>
    where TCallsite : notnull
{
    internal WindowSummary(
        double threshold,
        IReadOnlyList<KeptItem<TCallsite>> keptItems,
        IReadOnlyDictionary<TCallsite, CallsiteEstimate<TCallsite>> estimates)
    {
        this.Threshold = threshold;
        this.KeptItems = keptItems;
        this.Estimates = estimates;
    }

    /// <summary>
    /// Gets the window threshold <c>tau</c>, the <c>(k+1)</c>-th smallest key. It
    /// is <see cref="double.PositiveInfinity"/> when the reservoir never filled,
    /// in which case every admitted arrival was kept and its count is exact.
    /// </summary>
    public double Threshold { get; }

    /// <summary>
    /// Gets the items retained in this window's sample. A caller that buffered
    /// admitted events should emit exactly the buffered events whose tokens
    /// appear here, each stamped with its callsite's adjusted count.
    /// </summary>
    public IReadOnlyList<KeptItem<TCallsite>> KeptItems { get; }

    /// <summary>
    /// Gets the per-callsite estimates for the callsites present in the sample,
    /// keyed by callsite.
    /// </summary>
    public IReadOnlyDictionary<TCallsite, CallsiteEstimate<TCallsite>> Estimates { get; }
}
