// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor.Tests;

public class BottomFloorSamplerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveBudget(int budget)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BottomFloorSampler<string>(budget));
    }

    [Fact]
    public void InclusionProbability_StaysAccurateAndPositiveForATinyTheta()
    {
        // 1 - exp(-theta) underflows to exactly zero once theta drops below the
        // double epsilon, and the adjusted count divides by this value. A zero
        // would produce an infinite count estimate for the heaviest hitters,
        // which are exactly the callsites with the smallest theta.
        Assert.Equal(0.0, 1.0 - Math.Exp(-1e-20));

        foreach (var theta in new[] { 1e-20, 1e-12, 1e-8, 1e-5 })
        {
            var p = BottomFloorSampler<string>.InclusionProbability(theta);

            Assert.True(p > 0.0, FormattableString.Invariant($"theta={theta} produced {p}"));

            // The series is accurate to O(theta^4), far tighter than this bound.
            Assert.InRange(p / theta, 1.0 - (theta * 0.51), 1.0);
        }
    }

    [Fact]
    public void InclusionProbability_IsContinuousAcrossTheSeriesCutoff()
    {
        const double Cutoff = 1e-4;
        var below = BottomFloorSampler<string>.InclusionProbability(Numeric.BitDecrement(Cutoff));
        var above = BottomFloorSampler<string>.InclusionProbability(Cutoff);

        // Both forms must agree where they meet, otherwise the estimator would
        // step discontinuously as a callsite's weight crosses the cutoff.
        Assert.Equal(above, below, 15);
    }

    [Fact]
    public void UnderFullWindow_KeepsEverythingWithExactCounts()
    {
        var sampler = new BottomFloorSampler<string>(budget: 10, random: new Random(1));

        // Fewer arrivals than the budget: nothing is dropped.
        sampler.Offer("a");
        sampler.Offer("a");
        sampler.Offer("b");

        var summary = sampler.CloseWindow();

        Assert.Equal(double.PositiveInfinity, summary.Threshold);
        Assert.Equal(3, summary.KeptItems.Count);

        var a = summary.Estimates["a"];
        Assert.Equal(2, a.KeptCount);
        Assert.Equal(1.0, a.InclusionProbability);
        Assert.Equal(2.0, a.EstimatedCount);
        Assert.Equal(0.0, a.SquaredCoefficientOfVariation);
    }

    [Fact]
    public void FullWindow_SampleSizeEqualsBudget()
    {
        var sampler = new BottomFloorSampler<int>(budget: 32, random: new Random(2));

        for (var i = 0; i < 100_000; i++)
        {
            sampler.Offer(i % 50);
        }

        var summary = sampler.CloseWindow();

        Assert.Equal(32, summary.KeptItems.Count);
        Assert.True(summary.Threshold > 0.0 && Numeric.IsFinite(summary.Threshold));
    }

    [Fact]
    public void KeptItems_MatchLiveTokensFromOfferOutcomes()
    {
        var sampler = new BottomFloorSampler<int>(budget: 16, random: new Random(3));
        var live = new HashSet<long>();

        for (var i = 0; i < 5_000; i++)
        {
            var outcome = sampler.Offer(i % 20);
            if (!outcome.Admitted)
            {
                continue;
            }

            if (outcome.Evicted)
            {
                Assert.True(live.Remove(outcome.EvictedToken));
            }

            Assert.True(live.Add(outcome.Token));
        }

        var summary = sampler.CloseWindow();

        // The boundary item (the (k+1)-th key) is dropped at window close, so the
        // kept set is the live set minus exactly one token.
        var kept = new HashSet<long>(summary.KeptItems.Select(k => k.Token));
        Assert.Equal(sampler.Budget, kept.Count);
        Assert.True(kept.IsSubsetOf(live));
        Assert.Equal(1, live.Count - kept.Count);
    }

    [Fact]
    public void Cv2_EqualsClosedForm()
    {
        var sampler = new BottomFloorSampler<int>(budget: 40, random: new Random(4));

        for (var i = 0; i < 20_000; i++)
        {
            sampler.Offer(i % 8);
        }

        var summary = sampler.CloseWindow();

        foreach (var estimate in summary.Estimates.Values)
        {
            var expected = (1.0 - estimate.InclusionProbability) / estimate.KeptCount;
            Assert.Equal(expected, estimate.SquaredCoefficientOfVariation, 12);
            Assert.Equal(estimate.KeptCount / estimate.InclusionProbability, estimate.EstimatedCount, 9);
        }
    }

    [Fact]
    public void PerRecordAdjustedCount_SumsToTheCallsiteEstimatedCount()
    {
        // The two ways to read a window must agree: stamping 1 / inclusion on each
        // kept record (what the exporter does) must total EstimatedCount, which is
        // a per-callsite figure. Adding EstimatedCount once per record instead
        // overstates the count by KeptCount times.
        var sampler = new BottomFloorSampler<int>(budget: 40, random: new Random(11));

        for (var i = 0; i < 20_000; i++)
        {
            sampler.Offer(i % 8);
        }

        var summary = sampler.CloseWindow();

        var perRecordTotals = new Dictionary<int, double>();
        foreach (var kept in summary.KeptItems)
        {
            var inclusion = summary.Estimates[kept.Callsite].InclusionProbability;
            perRecordTotals[kept.Callsite] =
                perRecordTotals.TryGetValue(kept.Callsite, out var acc) ? acc + (1.0 / inclusion) : 1.0 / inclusion;
        }

        Assert.NotEmpty(summary.Estimates);
        foreach (var estimate in summary.Estimates.Values)
        {
            Assert.Equal(estimate.EstimatedCount, perRecordTotals[estimate.Callsite], 9);
        }
    }

    [Fact]
    public void UnseenWeight_IsLargestSurvivingWeight()
    {
        var sampler = new BottomFloorSampler<int>(budget: 40, random: new Random(5));

        for (var i = 0; i < 20_000; i++)
        {
            sampler.Offer(i % 8);
        }

        var summary = sampler.CloseWindow();

        var maxWeight = summary.Estimates.Values.Max(e => 1.0 / e.EstimatedCount);
        Assert.Equal(maxWeight, sampler.UnseenWeight, 12);
    }

    [Fact]
    public void ExtremeDraw_ProducesFiniteKeysAndEstimates()
    {
        // NextDouble() approaching 1 maps to u approaching 0, the largest key. The
        // sampler maps the uniform to (0, 1] so u is never 0 and the key -ln(u)/w
        // stays finite even at the extreme.
        var sampler = new BottomFloorSampler<int>(budget: 4, random: new AlmostOne());

        for (var i = 0; i < 50; i++)
        {
            sampler.Offer(i % 6);
        }

        var summary = sampler.CloseWindow();
        Assert.True(Numeric.IsFinite(summary.Threshold));
        Assert.All(summary.Estimates.Values, e =>
        {
            Assert.True(Numeric.IsFinite(e.EstimatedCount));
            Assert.True(e.EstimatedCount > 0.0);
        });
    }

    [Fact]
    public void CountEstimates_AreUnbiased_AcrossManyWindows()
    {
        var sampler = new BottomFloorSampler<string>(budget: 50, random: new Random(12345));

        var trueCounts = new Dictionary<string, int>
        {
            ["heavy"] = 1000,
            ["medium"] = 100,
            ["light"] = 10,
            ["rare"] = 1,
        };

        const int windows = 6000;
        var totalEstimated = new Dictionary<string, double>();
        var grandTotalEstimated = 0.0;

        for (var w = 0; w < windows; w++)
        {
            foreach (var pair in trueCounts)
            {
                for (var i = 0; i < pair.Value; i++)
                {
                    sampler.Offer(pair.Key);
                }
            }

            var summary = sampler.CloseWindow();
            foreach (var estimate in summary.Estimates.Values)
            {
                totalEstimated.TryGetValue(estimate.Callsite, out var acc);
                totalEstimated[estimate.Callsite] = acc + estimate.EstimatedCount;
                grandTotalEstimated += estimate.EstimatedCount;
            }
        }

        // Each callsite's mean estimated count should recover its true count.
        foreach (var pair in trueCounts)
        {
            var mean = (totalEstimated.TryGetValue(pair.Key, out var estimated) ? estimated : 0.0) / windows;
            var relativeError = Math.Abs(mean - pair.Value) / pair.Value;
            Assert.True(relativeError < 0.10, $"{pair.Key}: mean={mean}, true={pair.Value}, relErr={relativeError:F3}");
        }

        // The group total is unbiased too.
        var trueTotal = trueCounts.Values.Sum();
        var meanTotal = grandTotalEstimated / windows;
        Assert.True(Math.Abs(meanTotal - trueTotal) / trueTotal < 0.05);
    }

    [Fact]
    public void RareCallsite_IsRecalled_ThroughRarestSeenFloor()
    {
        var sampler = new BottomFloorSampler<string>(budget: 40, random: new Random(999));

        // Warm up so the feedback settles, then measure recall of the rare callsite.
        var recalled = 0;
        const int windows = 2000;
        for (var w = 0; w < windows; w++)
        {
            for (var i = 0; i < 2000; i++)
            {
                sampler.Offer("flood");
            }

            sampler.Offer("rare");

            var summary = sampler.CloseWindow();
            if (summary.Estimates.ContainsKey("rare"))
            {
                recalled++;
            }
        }

        // Equal coverage drives the rare callsite's recall far above its raw
        // frequency of 1/2001; the floor keeps it visible a large fraction of windows.
        var recall = (double)recalled / windows;
        Assert.True(recall > 0.3, $"rare recall was {recall:F3}");
    }

    [Fact]
    public void AdjustedCounts_RecoverTheArrivalCountAcrossManyWindows()
    {
        var rng = new Random(17);
        var callsites = Enumerable.Range(1, 12).Select(i => ($"App.Callsite{i:00}", i)).ToArray();
        var cdf = BuildZipfCdf(callsites.Length);

        var windows = new List<List<MyEvent>>();
        var arrivals = 0L;
        for (var w = 0; w < 200; w++)
        {
            var window = new List<MyEvent>();
            for (var n = 0; n < 1000; n++)
            {
                var u = rng.NextDouble();
                var index = Array.FindIndex(cdf, x => u <= x);
                var (category, eventId) = callsites[index < 0 ? cdf.Length - 1 : index];
                window.Add(new MyEvent(category, eventId));
                arrivals++;
            }

            windows.Add(window);
        }

        var estimated = 0.0;
        var exported = 0L;

        var sampler = new BottomFloorSampler<(string Category, int EventId)>(budget: 100);
        var buffered = new Dictionary<long, MyEvent>();

        foreach (var window in windows)
        {
            foreach (var item in window)
            {
                var outcome = sampler.Offer((item.Category, item.EventId));
                if (!outcome.Admitted)
                {
                    continue;
                }

                // Honour the eviction, so the buffer holds exactly the reservoir.
                if (outcome.Evicted)
                {
                    buffered.Remove(outcome.EvictedToken);
                }

                buffered[outcome.Token] = item;
            }

            var summary = sampler.CloseWindow();
            foreach (var kept in summary.KeptItems)
            {
                var estimate = summary.Estimates[kept.Callsite];

                // The per-record adjusted count is 1 / inclusion probability. Summed
                // over a callsite's kept records it reproduces estimate.EstimatedCount,
                // that callsite's estimated arrival count for the window.
                estimated += 1.0 / estimate.InclusionProbability;
                exported++;
            }

            // The next window starts from an empty reservoir, so nothing carries over.
            buffered.Clear();
        }

        // The run must actually subsample, or it proves nothing.
        Assert.True(exported < arrivals / 5, $"expected heavy subsampling, exported {exported} of {arrivals}");

        // And its adjusted counts must recover what was thrown away. Stamping
        // EstimatedCount per record instead would overshoot by roughly ninefold
        // here, so this tolerance is far tighter than that failure mode.
        var relativeError = Math.Abs(estimated - arrivals) / arrivals;
        Assert.True(relativeError < 0.05, $"relative error {relativeError:P2} exceeded 5% (estimated {estimated:F0}, arrivals {arrivals})");

        // The buffer is drained every window, so nothing accumulates across them.
        Assert.Empty(buffered);
    }

    private static double[] BuildZipfCdf(int count)
    {
        var cdf = new double[count];
        var total = 0.0;
        for (var i = 0; i < count; i++)
        {
            total += 1.0 / (i + 1);
            cdf[i] = total;
        }

        for (var i = 0; i < count; i++)
        {
            cdf[i] /= total;
        }

        return cdf;
    }

    private sealed class AlmostOne : Random
    {
        public override double NextDouble() => 1.0 - 1e-12;
    }

    private sealed class MyEvent
    {
        public MyEvent(string category, int eventId)
        {
            this.Category = category;
            this.EventId = eventId;
        }

        public string Category { get; }

        public int EventId { get; }
    }
}
