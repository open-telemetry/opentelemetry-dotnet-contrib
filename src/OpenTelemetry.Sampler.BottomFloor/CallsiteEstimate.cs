// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// The per-callsite estimate produced for one window: an unbiased count and the
/// quantities behind it, including the self-reported adequacy signal <c>cv2</c>.
/// </summary>
/// <typeparam name="TCallsite">The callsite key type.</typeparam>
internal readonly struct CallsiteEstimate<TCallsite>
    where TCallsite : notnull
{
    internal CallsiteEstimate(
        TCallsite callsite,
        long keptCount,
        double weight,
        double inclusionProbability,
        double estimatedCount,
        double squaredCoefficientOfVariation)
    {
        this.Callsite = callsite;
        this.KeptCount = keptCount;
        this.Weight = weight;
        this.InclusionProbability = inclusionProbability;
        this.EstimatedCount = estimatedCount;
        this.SquaredCoefficientOfVariation = squaredCoefficientOfVariation;
    }

    /// <summary>Gets the callsite this estimate describes.</summary>
    public TCallsite Callsite { get; }

    /// <summary>
    /// Gets the number of the callsite's arrivals retained in the sample, <c>m</c>.
    /// </summary>
    public long KeptCount { get; }

    /// <summary>
    /// Gets the sampling weight applied to the callsite this window, <c>w</c>.
    /// </summary>
    public double Weight { get; }

    /// <summary>
    /// Gets the per-arrival inclusion probability <c>1 - exp(-tau w)</c> the count
    /// is corrected by. It is one when the window's reservoir never filled.
    /// </summary>
    public double InclusionProbability { get; }

    /// <summary>
    /// Gets the unbiased Horvitz-Thompson count estimate <c>nhat = m / inclusion</c>,
    /// the sampler's estimate of how many times the callsite fired this window.
    /// <para/>
    /// This is a per-callsite total for the whole window, not a per-record value.
    /// Add it once per callsite, iterating <see cref="WindowSummary{TCallsite}.Estimates"/>.
    /// Code that iterates <see cref="WindowSummary{TCallsite}.KeptItems"/> is visiting
    /// records, and must stamp <c>1 / <see cref="InclusionProbability"/></c> instead;
    /// adding this value once per kept record overstates the count by
    /// <see cref="KeptCount"/> times.
    /// </summary>
    public double EstimatedCount { get; }

    /// <summary>
    /// Gets the squared coefficient of variation <c>(1 - inclusion) / m</c> of the
    /// count estimate. Small means the callsite is well sampled; a value above the
    /// operator's target <c>1 / m*</c> means the budget, not the estimator, is the limit.
    /// </summary>
    public double SquaredCoefficientOfVariation { get; }
}
