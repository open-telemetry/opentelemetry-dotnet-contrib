# Changelog - OpenTelemetry.Sampler.BottomFloor

## Unreleased

* Initial Bottom-Floor sampler: a fixed-memory, single-parameter bottom-k stream
  sampler with Horvitz-Thompson count correction, inverse-frequency feedback,
  the rarest-seen floor, and a per-callsite `cv2` adequacy signal.
* Added `BottomFloorLogExporter`, a `LogRecord` exporter that applies the
  sampler per export batch and forwards each window's kept sample stamped with
  its `otel.logs.adjusted_count` and adequacy signal. A count of one is omitted.
* Added `BottomFloorLogRecordProcessor`, a batching processor that wraps
  `BottomFloorLogExporter` so a single processor can be registered directly; its
  export size and interval set the sampling window. The export size must exceed
  the budget, otherwise the whole-stream budget would never bind.
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
