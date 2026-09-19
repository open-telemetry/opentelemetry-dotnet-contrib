// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
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
    private readonly string adjustedCountAttribute;
    private readonly string squaredCvAttribute;
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

        Guard.ThrowIfNull(options);

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

        this.sampler = new BottomFloorSampler<long>(options.Budget, random);
        this.adjustedCountAttribute = options.AdjustedCountAttribute;
        this.squaredCvAttribute = options.SquaredCoefficientOfVariationAttribute;
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

        foreach (var record in batch)
        {
            var callsite = ComputeCallsiteId(record.CategoryName, record.EventId);

            var outcome = this.sampler.Offer(callsite);
            if (outcome.Admitted)
            {
                if (outcome.Evicted)
                {
                    this.buffer.Remove(outcome.EvictedToken);
                }

                // The pooled record is cleared and reclaimed the moment the
                // enumerator advances past it, so a record the reservoir keeps must
                // be retained now as a self-contained copy.
                this.buffer[outcome.Token] = LogRecordRetention.Retain(record);
            }
        }

        var summary = this.sampler.CloseWindow();

        var kept = new LogRecord[summary.KeptItems.Count];
        var count = 0;
        foreach (var item in summary.KeptItems)
        {
            if (!this.buffer.TryGetValue(item.Token, out var record))
            {
                continue;
            }

            var estimate = summary.Estimates[item.Callsite];
            this.Stamp(record, new RecordStamp
            {
                StreamAdjustedCount = 1.0 / estimate.InclusionProbability,
                SquaredCoefficientOfVariation = estimate.SquaredCoefficientOfVariation,
            });

            kept[count++] = record;
        }

        this.buffer.Clear();

        if (count == 0)
        {
            return ExportResult.Success;
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

    private void Stamp(LogRecord record, in RecordStamp stamp)
    {
        var existing = record.Attributes;
        var attributes = new List<KeyValuePair<string, object?>>((existing?.Count ?? 0) + 2);
        if (existing != null)
        {
            attributes.AddRange(existing);
        }

        // An adjusted count of one means the record was fully included, so it
        // carries no information and is omitted. The variance companion is only
        // meaningful when the record was actually subsampled.
        if (stamp.StreamAdjustedCount != 1.0)
        {
            attributes.Add(new KeyValuePair<string, object?>(this.adjustedCountAttribute, stamp.StreamAdjustedCount));
            if (stamp.StreamAdjustedCount > 1.0)
            {
                attributes.Add(new KeyValuePair<string, object?>(this.squaredCvAttribute, stamp.SquaredCoefficientOfVariation));
            }
        }

        record.Attributes = attributes;
    }

    private struct RecordStamp
    {
        public double StreamAdjustedCount;
        public double SquaredCoefficientOfVariation;
    }
}
