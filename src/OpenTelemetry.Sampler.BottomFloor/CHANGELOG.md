# Changelog - OpenTelemetry.Sampler.BottomFloor

## Unreleased

* Initial Bottom-Floor sampler: a fixed-memory, single-parameter bottom-k stream
  sampler with Horvitz-Thompson count correction, inverse-frequency feedback,
  the rarest-seen floor, and a per-callsite `cv2` adequacy signal.
* Added `BottomFloorLogExporter`, a `LogRecord` exporter that applies the
  sampler per export batch and forwards each window's kept sample stamped with
  its `otel.logs.adjusted_count` and adequacy signal. A count of one is omitted
  and a count of zero marks a record forwarded only for span coverage.
* Added `BottomFloorLogRecordProcessor`, a batching processor that wraps
  `BottomFloorLogExporter` so a single processor can be registered directly; its
  export size and interval set the sampling window. The export size must exceed
  the budget, otherwise the whole-stream budget would never bind.
* Added opt-in per-span log coverage: when
  `BottomFloorLogSamplerOptions.MaxLogsPerSpanPerWindow` is greater than zero,
  each in-span record is additionally offered to a small ephemeral Bottom-Floor
  reservoir keyed by span id, seeded from the whole-stream sampler's converged
  weights, so a span keeps at most that many logs per window and its logs stay
  retrievable together while a chatty span cannot dominate. Records kept only
  for span coverage carry a separate `otel.span_logs.adjusted_count` estimator
  and a whole-stream count of zero. The option defaults to zero, so by default
  the budget alone bounds what the sampler forwards.
* The log sampler retains each selected record as a self-contained copy before
  the pooled record is reclaimed, so forwarded records keep their original
  attributes and scopes rather than only the sampler's stamped counts. The copy
  uses `UnsafeAccessor` where available and reflection otherwise; when neither
  can be bound the sampler forwards every record unsampled rather than emitting
  corrupted ones.
* The log exporter forwards its `ParentProvider` to the wrapped exporter, so a
  decorated exporter still observes the provider's `Resource`. The forwarding
  uses `UnsafeAccessor` on .NET 9 and later and reflection otherwise, and
  degrades to a no-op when neither can be bound.
