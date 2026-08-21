// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// The local Bottom-Floor sampler: a fixed-memory, single-parameter,
/// self-calibrating stream sampler for callsite-labelled events.
/// <para/>
/// Within a window each arrival is admitted into a bottom-k reservoir keyed by
/// an exponential priority <c>-ln(u) / w</c>. At <see cref="CloseWindow"/> the
/// <c>(k+1)</c>-th smallest key becomes the window threshold <c>tau</c>, the
/// <c>k</c> smallest keys form the kept sample, and each kept callsite is given
/// an unbiased Horvitz-Thompson count estimate <c>nhat = m / (1 - exp(-tau w))</c>.
/// Those counts set the next window's weights <c>w = 1 / nhat</c>, driving the
/// sampler toward equal coverage, and the largest surviving weight becomes the
/// rarest-seen floor applied to any callsite not seen in the previous window.
/// </summary>
/// <typeparam name="TCallsite">
/// The callsite key type, a hashable identifier of where an event originated
/// (for example a <c>(category, event id)</c> pair). Used as a dictionary key,
/// so it must implement value equality.
/// </typeparam>
/// <remarks>
/// Instances are not thread-safe. A caller that samples a concurrent stream
/// must serialize calls to <see cref="Offer"/> and <see cref="CloseWindow"/>.
/// </remarks>
public sealed class BottomFloorSampler<TCallsite>
    where TCallsite : notnull
{
    // A uniform draw resolves probabilities no finer than one part in 2^31: the
    // coarsest supported generator, .NET Framework's Random, yields about 31
    // random bits, so no draw can realize an inclusion probability below this.
    // Flooring inclusion here caps the adjusted count at 2^31 and keeps the
    // derived weight (1 / estimate) strictly positive, which for any realistic
    // sample size never binds because Horvitz-Thompson variance holds the
    // estimate near the actual arrival count.
    private const double MinInclusionProbability = 1.0 / (1L << 31);

    private readonly int budget;
    private readonly int capacity;
    private readonly Random random;
    private readonly Entry[] heap;

    private Dictionary<TCallsite, double> weights;
    private double unseenWeight;
    private int count;
    private long nextToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="BottomFloorSampler{TCallsite}"/> class.
    /// </summary>
    /// <param name="budget">
    /// The memory budget <c>k</c>: the number of items the window sample holds.
    /// This is the sampler's single user parameter and must be at least one.
    /// </param>
    /// <param name="random">
    /// The random source for the per-arrival draw. When <see langword="null"/>
    /// the shared <see cref="Random.Shared"/> instance is used. The sampler is
    /// not thread-safe regardless of the source, so a caller must still serialize
    /// <see cref="Offer"/> and <see cref="CloseWindow"/>; pass a seeded instance
    /// for reproducible sampling in tests.
    /// </param>
    public BottomFloorSampler(int budget, Random? random = null)
    {
        if (budget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), budget, "Budget must be at least one.");
        }

        this.budget = budget;
        this.capacity = budget + 1;
        this.random = random ?? Random.Shared;
        this.heap = new Entry[this.capacity];
        this.weights = new Dictionary<TCallsite, double>(budget);
        this.unseenWeight = 1.0;
    }

    /// <summary>
    /// Gets the memory budget <c>k</c>, the number of items the window sample holds.
    /// </summary>
    public int Budget => this.budget;

    /// <summary>
    /// Gets the rarest-seen floor weight applied to any callsite not present in
    /// the previous window's sample. It equals the largest surviving weight and
    /// puts a lower bound on the inclusion probability of newly appearing callsites.
    /// </summary>
    public double UnseenWeight => this.unseenWeight;

    /// <summary>
    /// Gets the weights learned by the previous window, keyed by callsite.
    /// <para/>
    /// <see cref="CloseWindow"/> replaces this dictionary wholesale rather than
    /// mutating it, and <see cref="Offer"/> only reads it, so the instance may be
    /// handed to a short-lived sampler that seeds itself from it without copying.
    /// </summary>
    internal Dictionary<TCallsite, double> CurrentWeights => this.weights;

    /// <summary>
    /// Offers one arrival of <paramref name="callsite"/> to the current window's
    /// reservoir. The decision is made before any expensive per-event work, so a
    /// caller should perform formatting or capture only for an admitted arrival.
    /// </summary>
    /// <param name="callsite">The callsite the arrival belongs to.</param>
    /// <returns>
    /// The outcome describing whether the arrival was admitted and whether an
    /// earlier admitted arrival was evicted to make room for it.
    /// </returns>
    public OfferOutcome Offer(TCallsite callsite)
    {
        var weight = this.weights.TryGetValue(callsite, out var w) ? w : this.unseenWeight;

        // The weight is always strictly positive: every stored weight derives from
        // a capped estimate (see MinInclusionProbability) and the unseen floor is
        // seeded positive, so the priority key below never divides by zero.
        Debug.Assert(weight > 0.0, "callsite weight must be strictly positive");

        // Non-cryptographic randomness is intentional: this is statistical sampling,
        // not a security context, and the fast path must stay cheap per arrival.
#pragma warning disable CA5394 // Do not use insecure randomness
        var u = 1.0 - this.random.NextDouble();
#pragma warning restore CA5394

        var key = -Math.Log(u) / weight;

        if (this.count < this.capacity)
        {
            var token = this.nextToken++;
            this.HeapPush(new Entry(key, callsite, token));
            return OfferOutcome.Admit(token);
        }

        if (key < this.heap[0].Key)
        {
            var token = this.nextToken++;
            var evicted = this.heap[0];
            this.heap[0] = new Entry(key, callsite, token);
            this.HeapSiftDown(0);
            return OfferOutcome.AdmitEvicting(token, evicted.Token);
        }

        return default;
    }

    /// <summary>
    /// Closes the current window: reads the threshold, forms the kept sample,
    /// computes each kept callsite's unbiased count estimate and adequacy signal,
    /// and rolls the derived weights forward as the next window's state.
    /// </summary>
    /// <returns>The summary of the window that was just closed.</returns>
    public WindowSummary<TCallsite> CloseWindow()
    {
        double threshold;
        int sampleSize;

        if (this.count <= this.budget)
        {
            // The reservoir never filled, so every admitted arrival is kept and
            // its count is exact: an infinite threshold gives an inclusion
            // probability of one and leaves the Horvitz-Thompson count uncorrected.
            threshold = double.PositiveInfinity;
            sampleSize = this.count;
        }
        else
        {
            // The reservoir holds k+1 keys; the largest is the boundary and the
            // remaining k form the sample.
            var boundary = this.HeapPopMax();
            threshold = boundary.Key;
            sampleSize = this.count;
        }

        var counts = new Dictionary<TCallsite, long>(sampleSize);
        var keptItems = new KeptItem<TCallsite>[sampleSize];
        for (var i = 0; i < sampleSize; i++)
        {
            var entry = this.heap[i];
            keptItems[i] = new KeptItem<TCallsite>(entry.Token, entry.Callsite);
            counts[entry.Callsite] = counts.TryGetValue(entry.Callsite, out var c) ? c + 1 : 1;
        }

        var estimates = new Dictionary<TCallsite, CallsiteEstimate<TCallsite>>(counts.Count);
        var nextWeights = new Dictionary<TCallsite, double>(counts.Count);
        var nextUnseen = 0.0;
        foreach (var pair in counts)
        {
            var callsite = pair.Key;
            var m = pair.Value;
            var weight = this.weights.TryGetValue(callsite, out var w) ? w : this.unseenWeight;

            var theta = threshold * weight;
            var inclusion = Math.Max(InclusionProbability(theta), MinInclusionProbability);
            var estimatedCount = m / inclusion;
            var cv2 = (1.0 - inclusion) / m;
            estimates[callsite] = new CallsiteEstimate<TCallsite>(callsite, m, weight, inclusion, estimatedCount, cv2);

            var nextWeight = 1.0 / estimatedCount;
            nextWeights[callsite] = nextWeight;
            if (nextWeight > nextUnseen)
            {
                nextUnseen = nextWeight;
            }
        }

        this.weights = nextWeights;
        this.unseenWeight = counts.Count > 0 ? nextUnseen : 1.0;
        this.count = 0;

        return new WindowSummary<TCallsite>(threshold, keptItems, estimates);
    }

    /// <summary>
    /// Seeds this sampler's starting weights from a table another sampler has
    /// already learned, giving an ephemeral per-span sampler the same
    /// equal-coverage bias the whole-stream sampler has converged on.
    /// <para/>
    /// The dictionary is shared, not copied: a per-span sampler is created for
    /// every span in a window, so copying the stream's weight table into each one
    /// would cost far more than the sampling it performs. Sharing is safe because
    /// neither sampler ever mutates the table in place.
    /// <para/>
    /// Weights only steer which arrivals are kept. The Horvitz-Thompson estimate
    /// divides by an inclusion probability derived from these same weights, so
    /// the seed changes variance, never unbiasedness.
    /// </summary>
    /// <param name="initialWeights">The per-callsite starting weights to share.</param>
    /// <param name="initialUnseenWeight">The starting rarest-seen floor weight.</param>
    internal void SeedWeights(Dictionary<TCallsite, double> initialWeights, double initialUnseenWeight)
    {
        this.weights = initialWeights;

        // The priority key divides by the weight, so a non-positive or
        // non-finite floor would poison every unseen callsite.
        this.unseenWeight = initialUnseenWeight > 0.0 && double.IsFinite(initialUnseenWeight)
            ? initialUnseenWeight
            : 1.0;
    }

    private static double InclusionProbability(double theta)
    {
        if (double.IsPositiveInfinity(theta))
        {
            return 1.0;
        }

        // 1 - exp(-theta) loses precision and can underflow to exactly zero for a
        // small theta, which is the heavy-hitter regime (tiny weight) that then
        // divides into the count estimate. A truncated series keeps full accuracy
        // and stays strictly positive there; elsewhere the direct form is exact.
        if (theta < 1e-4)
        {
            return theta * (1.0 - (theta * (0.5 - (theta / 6.0))));
        }

        return 1.0 - Math.Exp(-theta);
    }

    private void HeapPush(in Entry entry)
    {
        var i = this.count++;
        this.heap[i] = entry;
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (this.heap[parent].Key >= this.heap[i].Key)
            {
                break;
            }

            (this.heap[parent], this.heap[i]) = (this.heap[i], this.heap[parent]);
            i = parent;
        }
    }

    private Entry HeapPopMax()
    {
        var max = this.heap[0];
        this.count--;
        this.heap[0] = this.heap[this.count];
        this.heap[this.count] = default;
        this.HeapSiftDown(0);
        return max;
    }

    private void HeapSiftDown(int i)
    {
        while (true)
        {
            var left = (2 * i) + 1;
            var right = (2 * i) + 2;
            var largest = i;

            if (left < this.count && this.heap[left].Key > this.heap[largest].Key)
            {
                largest = left;
            }

            if (right < this.count && this.heap[right].Key > this.heap[largest].Key)
            {
                largest = right;
            }

            if (largest == i)
            {
                break;
            }

            (this.heap[largest], this.heap[i]) = (this.heap[i], this.heap[largest]);
            i = largest;
        }
    }

    private readonly struct Entry
    {
        public Entry(double key, TCallsite callsite, long token)
        {
            this.Key = key;
            this.Callsite = callsite;
            this.Token = token;
        }

        public double Key { get; }

        public TCallsite Callsite { get; }

        public long Token { get; }
    }
}
