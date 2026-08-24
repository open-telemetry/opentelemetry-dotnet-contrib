// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// A batching <see cref="LogRecord"/> processor that applies the local
/// Bottom-Floor sampler to each window before forwarding the kept sample to an
/// inner exporter. It is the ready-to-register form of
/// <see cref="BottomFloorLogExporter"/>: register this single processor instead
/// of wrapping the exporter in a <see cref="BatchLogRecordExportProcessor"/> by
/// hand.
/// <para/>
/// It derives from <see cref="BatchLogRecordExportProcessor"/>, so the sampler
/// runs on the single background export thread and needs no synchronization.
/// Note that the pooled records it is handed are reclaimed as the batch
/// enumerator advances past them, so any record the sampler retains past its
/// visit must first be copied; that is what the exporter does.
/// <para/>
/// One export is one sampling window. The window closes at the earlier of two
/// bounds: it holds <c>maxExportBatchSize</c> records (the export size) or
/// <c>scheduledDelayMilliseconds</c> elapse (the interval). Those two knobs set
/// the sampling window; <see cref="BottomFloorLogSamplerOptions.Budget"/> sets
/// how many records that window keeps. The export size must exceed the budget,
/// since a window no larger than the budget keeps every record it holds.
/// </summary>
public sealed class BottomFloorLogRecordProcessor : BatchLogRecordExportProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BottomFloorLogRecordProcessor"/> class.
    /// </summary>
    /// <param name="exporter">
    /// The exporter that receives the kept, adjusted-count-stamped records. The
    /// processor takes ownership of it and disposes it.
    /// </param>
    /// <param name="options">The sampler options, including the budget.</param>
    /// <param name="maxExportBatchSize">
    /// The export size: the maximum number of records a single window holds
    /// before it closes and is sampled. Must be greater than
    /// <see cref="BottomFloorLogSamplerOptions.Budget"/>, since a window no larger
    /// than the budget keeps everything it holds. The default is 2048.
    /// </param>
    /// <param name="scheduledDelayMilliseconds">
    /// The interval: the maximum time in milliseconds a window stays open before
    /// it closes and is sampled, even when it has not reached the export size.
    /// The default is 5000.
    /// </param>
    /// <param name="maxQueueSize">
    /// The maximum number of records buffered awaiting a window close. Records
    /// offered while the buffer is full are dropped before the sampler sees them,
    /// so keep it above the export size. The default is 4096.
    /// </param>
    /// <param name="exporterTimeoutMilliseconds">
    /// How long a single export may run before it is cancelled. The default is 30000.
    /// </param>
    public BottomFloorLogRecordProcessor(
        BaseExporter<LogRecord> exporter,
        BottomFloorLogSamplerOptions options,
        int maxExportBatchSize = 2048,
        int scheduledDelayMilliseconds = 5000,
        int maxQueueSize = 4096,
        int exporterTimeoutMilliseconds = 30000)

        // Ownership of the wrapping exporter transfers to the base batch
        // processor, which disposes it; the analyzer cannot see that transfer.
#pragma warning disable CA2000 // Dispose objects before losing scope
        : base(
            CreateSamplingExporter(exporter, options, maxExportBatchSize),
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
#pragma warning restore CA2000 // Dispose objects before losing scope
    {
    }

    private static BottomFloorLogExporter CreateSamplingExporter(
        BaseExporter<LogRecord> exporter,
        BottomFloorLogSamplerOptions options,
        int maxExportBatchSize)
    {
        Guard.ThrowIfNull(options);

        // One export batch is one sampling window. A window no larger than the
        // budget keeps every record it holds, so this combination would forward
        // the stream unsampled while still paying the sampler's cost. Rejecting it
        // here turns a silent no-op into a wiring error.
        if (maxExportBatchSize <= options.Budget)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExportBatchSize),
                maxExportBatchSize,
                FormattableString.Invariant(
                    $"The export batch size must be greater than the sampling budget ({options.Budget}); otherwise a window keeps every record it holds and no sampling occurs."));
        }

        return new BottomFloorLogExporter(exporter, options);
    }
}
