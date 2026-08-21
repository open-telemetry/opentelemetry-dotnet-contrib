// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Sampler.BottomFloor;

/// <summary>
/// Options for <see cref="BottomFloorLogExporter"/>.
/// </summary>
public sealed class BottomFloorLogSamplerOptions
{
    /// <summary>
    /// Gets or sets the sampling budget <c>k</c>: the number of log records kept
    /// per window. This is the single knob of the Bottom-Floor sampler. The
    /// reservoir uses <c>k + 1</c> slots of memory and keeps the <c>k</c>
    /// highest-priority arrivals each window. The default is <c>100</c>.
    /// <para/>
    /// A window is one batch delivered to the exporter, so the batch processor's
    /// maximum export batch size should exceed the budget for sampling to take
    /// effect; a window no larger than the budget keeps every record it holds.
    /// </summary>
    public int Budget { get; set; } = 100;

    /// <summary>
    /// Gets or sets the attribute name under which each emitted record carries
    /// its adjusted count: the reciprocal of the record's inclusion probability,
    /// so that summing the adjusted counts of a callsite's kept records recovers
    /// the callsite's unbiased arrival count. The count is omitted when it equals
    /// one, since a fully included record carries no correction; a missing count
    /// therefore reads as one. A count of zero marks a record forwarded only for
    /// span coverage, which must not contribute to whole-stream aggregation. The
    /// default is <c>otel.logs.adjusted_count</c>.
    /// </summary>
    public string AdjustedCountAttribute { get; set; } = "otel.logs.adjusted_count";

    /// <summary>
    /// Gets or sets the attribute name under which each emitted record carries
    /// the squared coefficient of variation of its callsite's count estimate, an
    /// adequacy signal that is near zero when the estimate is well supported and
    /// large when it rests on few kept records. It accompanies a stream count
    /// greater than one and is omitted otherwise. The default is
    /// <c>otel.logs.cv2</c>.
    /// </summary>
    public string SquaredCoefficientOfVariationAttribute { get; set; } = "otel.logs.cv2";

    /// <summary>
    /// Gets or sets the per-span coverage budget: the maximum number of log
    /// records kept for each span per window by the span-coverage reservoir, on
    /// top of the whole-stream sample. Within a window each in-span record is
    /// offered to both the stream sample and a small ephemeral Bottom-Floor
    /// sampler for its span, seeded from the weights the stream sampler has
    /// already learned, so a span's logs stay retrievable together while a chatty
    /// span cannot exceed this bound. The default is <c>0</c>, which disables
    /// span coverage and samples the stream only.
    /// <para/>
    /// Setting this above zero means <see cref="Budget"/> no longer bounds the
    /// output on its own: a window may forward up to <see cref="Budget"/> records
    /// plus this many for each distinct span it contains. Size it against the
    /// number of spans a window is expected to hold.
    /// </summary>
    public int MaxLogsPerSpanPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the attribute name under which a record kept for span
    /// coverage carries its per-span adjusted count: the reciprocal of its
    /// inclusion probability within its span, a separate estimator from the
    /// stream <see cref="AdjustedCountAttribute"/>. The count is omitted when it
    /// equals one; a count of zero marks an in-span record the span sample did not
    /// keep, so per-span aggregation stays unbiased; the count is absent entirely
    /// for records out of span or when span coverage is disabled. A record kept
    /// only for span coverage carries a stream count of zero and this per-span
    /// count, so it does not bias the whole-stream estimate. The default is
    /// <c>otel.span_logs.adjusted_count</c>.
    /// </summary>
    public string SpanAdjustedCountAttribute { get; set; } = "otel.span_logs.adjusted_count";
}
