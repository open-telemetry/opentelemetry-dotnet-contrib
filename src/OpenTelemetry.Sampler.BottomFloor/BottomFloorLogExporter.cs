// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// A <see cref="LogRecord"/> exporter that applies the local Bottom-Floor
/// sampler to each exported batch and forwards only the kept sample to an inner
/// exporter, stamping every forwarded record with its adjusted count.
/// <para/>
/// One export batch is one sampling window, so the exporter must be driven by a
/// batching processor: register it as
/// <c>new BatchLogRecordExportProcessor(new BottomFloorLogExporter(inner, options), maxExportBatchSize: ...)</c>
/// with a batch size larger than the budget. A batching processor hands the
/// whole batch to <see cref="Export"/> at once, so the sampler can weigh every
/// record in the window against the others before choosing which to keep. The
/// per-window feedback that drives the sampler toward equal coverage persists in
/// the exporter across batches.
/// <para/>
/// When span coverage is enabled, each in-span record is additionally offered to
/// a small ephemeral Bottom-Floor sampler for its span, so a span keeps at most
/// <see cref="BottomFloorLogSamplerOptions.MaxLogsPerSpanPerWindow"/> records per
/// window. Those per-span samplers are keyed by span id alone and seeded from the
/// weights the whole-stream sampler has already learned, so they hold no state of
/// their own beyond the window. Their kept records carry a separate per-span
/// adjusted count.
/// <para/>
/// With span coverage enabled the forwarded record count is bounded by
/// <see cref="BottomFloorLogSamplerOptions.Budget"/> plus
/// <see cref="BottomFloorLogSamplerOptions.MaxLogsPerSpanPerWindow"/> for each
/// distinct span in the window, not by the budget alone.
/// <para/>
/// Because a record delivered to a batch exporter is rented from the shared pool
/// and reclaimed as the batch is enumerated, each selected record is retained as
/// a self-contained copy through <see cref="LogRecordRetention"/> before it is
/// forwarded, preserving its original attributes and scopes.
/// <para/>
/// The inner exporter is handed this exporter's <c>ParentProvider</c> on first
/// export, so exporters that resolve their <c>Resource</c> from the provider work
/// when wrapped.
/// </summary>
public sealed class BottomFloorLogExporter : BaseExporter<LogRecord>
{
    private readonly BaseExporter<LogRecord> innerExporter;
    private readonly BottomFloorSampler<long> sampler;
    private readonly Random? random;
    private readonly string adjustedCountAttribute;
    private readonly string squaredCvAttribute;
    private readonly int maxLogsPerSpan;
    private readonly string spanAdjustedCountAttribute;
    private readonly Dictionary<long, LogRecord> buffer = new();
    private bool parentProviderPropagated;

    /// <summary>
    /// Initializes a new instance of the <see cref="BottomFloorLogExporter"/> class.
    /// </summary>
    /// <param name="innerExporter">
    /// The exporter that receives the kept, adjusted-count-stamped records. The
    /// Bottom-Floor exporter takes ownership of it and disposes it.
    /// </param>
    /// <param name="options">The sampler options.</param>
    public BottomFloorLogExporter(BaseExporter<LogRecord> innerExporter, BottomFloorLogSamplerOptions options)
        : this(innerExporter, options, random: null)
    {
    }

    internal BottomFloorLogExporter(BaseExporter<LogRecord> innerExporter, BottomFloorLogSamplerOptions options, Random? random)
    {
        this.innerExporter = innerExporter ?? throw new ArgumentNullException(nameof(innerExporter));

        ArgumentNullException.ThrowIfNull(options);

        if (options.Budget < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Budget, "Budget must be at least one.");
        }

        if (string.IsNullOrEmpty(options.AdjustedCountAttribute))
        {
            throw new ArgumentException("AdjustedCountAttribute must not be null or empty.", nameof(options));
        }

        if (string.IsNullOrEmpty(options.SquaredCoefficientOfVariationAttribute))
        {
            throw new ArgumentException("SquaredCoefficientOfVariationAttribute must not be null or empty.", nameof(options));
        }

        if (options.MaxLogsPerSpanPerWindow < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxLogsPerSpanPerWindow, "MaxLogsPerSpanPerWindow must not be negative.");
        }

        if (options.MaxLogsPerSpanPerWindow > 0 && string.IsNullOrEmpty(options.SpanAdjustedCountAttribute))
        {
            throw new ArgumentException("SpanAdjustedCountAttribute must not be null or empty.", nameof(options));
        }

        this.random = random;
        this.sampler = new BottomFloorSampler<long>(options.Budget, random);
        this.adjustedCountAttribute = options.AdjustedCountAttribute;
        this.squaredCvAttribute = options.SquaredCoefficientOfVariationAttribute;
        this.maxLogsPerSpan = options.MaxLogsPerSpanPerWindow;
        this.spanAdjustedCountAttribute = options.SpanAdjustedCountAttribute;
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        this.PropagateParentProvider();

        // Sampling means holding selected records past the enumeration visit that
        // delivers them, which is only safe with a self-contained copy. If the
        // SDK's copy primitive cannot be bound, forward the window untouched: the
        // stream goes unsampled, but no record loses its attributes or scopes.
        if (!LogRecordRetention.CanClone)
        {
            return this.innerExporter.Export(batch);
        }

        var spans = this.maxLogsPerSpan > 0 ? new Dictionary<ActivitySpanId, SpanCoverage>() : null;

        // Retained copies of in-span records that were forwarded, whether the span
        // reservoir kept them or not. A forwarded in-span record the span sample
        // did not keep must still carry a per-span count of zero so it does not
        // bias per-span aggregation.
        var spanCandidates = spans != null ? new HashSet<LogRecord>() : null;

        foreach (var record in batch)
        {
            var callsite = ComputeCallsiteId(record.CategoryName, record.EventId);

            // The pooled record is cleared and reclaimed the moment the enumerator
            // advances past it, so any record either reservoir keeps must be
            // retained now as a self-contained copy. One copy per source record is
            // shared by both reservoirs so a record kept by both maps to a single
            // forwarded record carrying both counts.
            LogRecord? retained = null;

            var outcome = this.sampler.Offer(callsite);
            if (outcome.Admitted)
            {
                retained = LogRecordRetention.Retain(record);
                if (outcome.Evicted)
                {
                    this.buffer.Remove(outcome.EvictedToken);
                }

                this.buffer[outcome.Token] = retained;
            }

            if (spans != null && record.SpanId != default)
            {
                var coverage = this.GetOrCreateSpanCoverage(spans, record.SpanId);
                var spanOutcome = coverage.Sampler.Offer(callsite);
                if (spanOutcome.Admitted)
                {
                    retained ??= LogRecordRetention.Retain(record);
                    if (spanOutcome.Evicted)
                    {
                        coverage.Buffer.Remove(spanOutcome.EvictedToken);
                    }

                    coverage.Buffer[spanOutcome.Token] = retained;
                }

                if (retained != null)
                {
                    spanCandidates!.Add(retained);
                }
            }
        }

        var summary = this.sampler.CloseWindow();

        // Reference equality is intended: LogRecord does not override equality, so
        // a record selected by both reservoirs maps to a single stamp.
        var keeps = new Dictionary<LogRecord, RecordStamp>();
        foreach (var item in summary.KeptItems)
        {
            if (!this.buffer.TryGetValue(item.Token, out var record))
            {
                continue;
            }

            var estimate = summary.Estimates[item.Callsite];
            keeps[record] = new RecordStamp
            {
                StreamAdjustedCount = 1.0 / estimate.InclusionProbability,
                SquaredCoefficientOfVariation = estimate.SquaredCoefficientOfVariation,
                SpanAdjustedCount = double.NaN,
            };
        }

        this.buffer.Clear();

        if (spans != null)
        {
            CloseSpans(spans, keeps);

            // Any forwarded in-span record the span sample did not keep contributes
            // zero to its span's estimate; a missing count would otherwise read as
            // one and inflate the per-span aggregation.
            foreach (var candidate in spanCandidates!)
            {
                if (keeps.TryGetValue(candidate, out var stamp) && double.IsNaN(stamp.SpanAdjustedCount))
                {
                    stamp.SpanAdjustedCount = 0.0;
                    keeps[candidate] = stamp;
                }
            }
        }

        if (keeps.Count == 0)
        {
            return ExportResult.Success;
        }

        var kept = new LogRecord[keeps.Count];
        var count = 0;
        foreach (var pair in keeps)
        {
            this.Stamp(pair.Key, pair.Value);
            kept[count++] = pair.Key;
        }

        return this.innerExporter.Export(new Batch<LogRecord>(kept, count));
    }

    /// <summary>
    /// Computes the stable callsite identity used to key the sampler. It is a
    /// 64-bit FNV-1a hash over the log category, the numeric event id, and the
    /// event name, which together identify the emitting statement independently
    /// of the formatted message.
    /// </summary>
    /// <param name="categoryName">The log category name.</param>
    /// <param name="eventId">The log event id.</param>
    /// <returns>The callsite identity.</returns>
    internal static long ComputeCallsiteId(string? categoryName, EventId eventId)
    {
        const ulong FnvOffset = 0xcbf29ce484222325UL;
        const ulong FnvPrime = 0x100000001b3UL;

        var hash = FnvOffset;
        HashBytes(ref hash, categoryName);
        hash = (hash ^ 0x00) * FnvPrime;

        var id = unchecked((uint)eventId.Id);
        for (var shift = 0; shift < 32; shift += 8)
        {
            hash = (hash ^ (byte)(id >> shift)) * FnvPrime;
        }

        hash = (hash ^ 0x00) * FnvPrime;
        HashBytes(ref hash, eventId.Name);

        return unchecked((long)hash);

        static void HashBytes(ref ulong hash, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (var b in Encoding.UTF8.GetBytes(text))
            {
                hash = (hash ^ b) * FnvPrime;
            }
        }
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds) =>
        this.innerExporter.ForceFlush(timeoutMilliseconds);

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds) =>
        this.innerExporter.Shutdown(timeoutMilliseconds);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.innerExporter.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void CloseSpans(Dictionary<ActivitySpanId, SpanCoverage> spans, Dictionary<LogRecord, RecordStamp> keeps)
    {
        foreach (var coverage in spans.Values)
        {
            var summary = coverage.Sampler.CloseWindow();

            foreach (var item in summary.KeptItems)
            {
                if (!coverage.Buffer.TryGetValue(item.Token, out var record))
                {
                    continue;
                }

                var spanAdjusted = 1.0 / summary.Estimates[item.Callsite].InclusionProbability;
                if (keeps.TryGetValue(record, out var existing))
                {
                    existing.SpanAdjustedCount = spanAdjusted;
                    keeps[record] = existing;
                }
                else
                {
                    keeps[record] = new RecordStamp
                    {
                        StreamAdjustedCount = 0.0,
                        SquaredCoefficientOfVariation = double.NaN,
                        SpanAdjustedCount = spanAdjusted,
                    };
                }
            }
        }
    }

    private void PropagateParentProvider()
    {
        if (this.parentProviderPropagated)
        {
            return;
        }

        // The SDK assigns ParentProvider after construction, so this is resolved
        // on first export rather than in the constructor. It is attempted once
        // either way: a null provider means nothing is wired above us.
        this.parentProviderPropagated = true;

        var provider = this.ParentProvider;
        if (provider != null)
        {
            ParentProviderPropagation.TrySet(this.innerExporter, provider);
        }
    }

    private SpanCoverage GetOrCreateSpanCoverage(Dictionary<ActivitySpanId, SpanCoverage> spans, ActivitySpanId spanId)
    {
        if (spans.TryGetValue(spanId, out var coverage))
        {
            return coverage;
        }

        // Seed the span's reservoir from the weights the whole-stream sampler has
        // already learned, so a callsite that floods the stream does not also
        // crowd out rarer callsites inside the span. The weight table is shared
        // rather than copied, which keeps this cheap no matter how many distinct
        // spans a single window holds.
        var spanSampler = new BottomFloorSampler<long>(this.maxLogsPerSpan, this.random);
        spanSampler.SeedWeights(this.sampler.CurrentWeights, this.sampler.UnseenWeight);

        coverage = new SpanCoverage(spanSampler);
        spans[spanId] = coverage;
        return coverage;
    }

    private void Stamp(LogRecord record, in RecordStamp stamp)
    {
        var existing = record.Attributes;
        var attributes = new List<KeyValuePair<string, object?>>((existing?.Count ?? 0) + 3);
        if (existing != null)
        {
            attributes.AddRange(existing);
        }

        // The stream estimator's count is one exactly when the record was fully
        // included, so that carries no information and is omitted; a count of zero
        // marks a span-only record that must not bias whole-stream aggregation and
        // is always emitted. The variance companion is only meaningful when the
        // record was actually subsampled.
        if (!double.IsNaN(stamp.StreamAdjustedCount) && stamp.StreamAdjustedCount != 1.0)
        {
            attributes.Add(new KeyValuePair<string, object?>(this.adjustedCountAttribute, stamp.StreamAdjustedCount));
            if (stamp.StreamAdjustedCount > 1.0)
            {
                attributes.Add(new KeyValuePair<string, object?>(this.squaredCvAttribute, stamp.SquaredCoefficientOfVariation));
            }
        }

        // The per-span estimator follows the same convention: omit a count of one,
        // emit a count of zero for an in-span record the span sample did not keep,
        // and omit entirely when the record is out of span or span coverage is off.
        if (!double.IsNaN(stamp.SpanAdjustedCount) && stamp.SpanAdjustedCount != 1.0)
        {
            attributes.Add(new KeyValuePair<string, object?>(this.spanAdjustedCountAttribute, stamp.SpanAdjustedCount));
        }

        record.Attributes = attributes;
    }

    private struct RecordStamp
    {
        public double StreamAdjustedCount;
        public double SquaredCoefficientOfVariation;
        public double SpanAdjustedCount;
    }

    private sealed class SpanCoverage
    {
        public SpanCoverage(BottomFloorSampler<long> sampler)
        {
            this.Sampler = sampler;
            this.Buffer = new Dictionary<long, LogRecord>();
        }

        public BottomFloorSampler<long> Sampler { get; }

        public Dictionary<long, LogRecord> Buffer { get; }
    }
}
