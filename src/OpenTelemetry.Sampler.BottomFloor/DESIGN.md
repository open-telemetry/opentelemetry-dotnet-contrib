# Bottom-Floor log sampler design

This document describes the design implemented by the
`OpenTelemetry.Sampler.BottomFloor` component in this change. See `README.md`
for usage.

## Scope

The component provides:

* a fixed-memory, per-window sampler for callsite-labelled events;
* an OpenTelemetry log exporter that applies the sampler to export batches;
* a batching log processor that captures the context needed by the exporter;
  and
* an optional bounded sample of logs from each recording span.

The component samples logs and estimates log counts. It does not make tracing
sampling decisions, export spans, coordinate sampling across processes, or
transport sampling feedback.

## Local sampler

`BottomFloorSampler<TCallsite>` is independent of OpenTelemetry types. Its only
required setting is the per-window budget `k`.

For each arrival of callsite `c`, `Offer` draws a uniform random value `u` and
computes an exponential priority:

```text
priority = -ln(u) / w_c
```

The sampler retains the `k + 1` arrivals with the smallest priorities. `Offer`
returns a token for each admitted arrival and, when necessary, the token of an
earlier arrival that was evicted. This lets a caller maintain a payload buffer
without storing payloads in the generic sampler.

When `CloseWindow` is called:

* if the window contains at most `k` arrivals, every arrival is retained and
  its inclusion probability is one;
* otherwise, the largest of the `k + 1` priorities becomes the threshold and
  the remaining `k` arrivals form the sample;
* for each retained callsite, the inclusion probability is
  `1 - exp(-threshold * w_c)`;
* its estimated count is the number of retained arrivals divided by that
  inclusion probability; and
* its squared coefficient of variation is
  `(1 - inclusion probability) / retained count`.

The estimated counts set the next window's weights:

```text
next weight = 1 / estimated count
```

This reduces the sampling weight of frequent callsites. The largest surviving
weight becomes the next window's unseen-callsite weight, preserving a higher
chance of inclusion for newly observed and rare callsites. If a window is
empty, the unseen-callsite weight resets to one.

The sampler is not thread-safe. Calls to `Offer` and `CloseWindow` must be
serialized.

## OpenTelemetry log integration

`BottomFloorLogExporter` wraps an inner `BaseExporter<LogRecord>`. Each batch
passed to `Export` is one sampling window. It:

1. derives a stable callsite ID from the record's category, event ID, and event
   name using a 64-bit FNV-1a hash;
2. offers each record to a `BottomFloorSampler<long>`;
3. retains copies of admitted records while honoring later evictions;
4. closes the window and stamps retained records with sampling metadata; and
5. forwards the retained batch to the inner exporter.

The message body is not part of the callsite ID because the category and event
ID identify the logging statement without requiring message formatting.

`BottomFloorLogRecordProcessor` packages the exporter inside
`BatchLogRecordExportProcessor`. The maximum export batch size and scheduled
delay determine when a window closes. The export batch size should exceed the
sampling budget; otherwise the entire window is retained.

The processor owns the wrapping exporter, and the wrapping exporter owns the
inner exporter.

## Emitted count estimates

Each stream-sampled record is assigned:

```text
otel.logs.adjusted_count = 1 / inclusion probability
```

Summing this value for the retained records of a callsite estimates that
callsite's original arrival count. A value of one is omitted because the record
was fully included. When a record was retained only by per-span coverage, the
stream adjusted count is zero so it does not inflate the stream estimate.

For records that were subsampled, `otel.logs.cv2` contains the squared
coefficient of variation of the callsite estimate.

Both attribute names are configurable through
`BottomFloorLogSamplerOptions`.

## Log record retention

Log records delivered by the batching pipeline may be pooled and reclaimed as
the input batch is enumerated. The exporter therefore creates a self-contained
copy when either the stream sampler or a per-span sampler admits a record.

When both samplers admit the same source record, they share one retained copy.
The copied record preserves the data needed by the inner exporter, including
attributes and instrumentation scope.

On .NET 8 and later, the copy is created by calling the SDK's internal
`LogRecord.Copy()` through `UnsafeAccessor`. Older targets use a reflection
fallback because `UnsafeAccessor` is unavailable.

If neither mechanism can be bound — for example because a future SDK renames or
removes the method — the exporter degrades to forwarding every batch unsampled
rather than emitting records whose contents have been recycled. Correctness is
preserved at the cost of the sampling benefit.

## Parent provider propagation

The SDK assigns `ParentProvider` to the registered exporter after construction,
and exporters such as OTLP read the provider's `Resource` from it. Because the
wrapping exporter is what the SDK sees, it forwards its own `ParentProvider` to
the wrapped exporter on first export, so a decorated exporter behaves as it
would undecorated.

The setter is internal to the SDK and there is no supported alternative, so it is
reached directly: through `UnsafeAccessor` on .NET 9 and later, and through
reflection on earlier targets. .NET 9 is the floor rather than .NET 8 because the
property is declared on the generic `BaseExporter<T>`, and `UnsafeAccessor` only
gained support for generic types in .NET 9. The accessor has to be declared on a
generic *type* whose parameters match the declaring type's; writing it as a
generic *method* that takes a `BaseExporter<T>` compiles but throws
`MissingMethodException` at run time.

As with the record copy, both mechanisms depend on an SDK implementation detail,
so both degrade to a no-op if the setter cannot be reached. The wrapped exporter
then keeps whatever provider it already had instead of the export failing.

### Alternatives considered

`BaseExporter<T>` exposes no supported seam for this. Its `ParentProvider` setter
is `internal`, there is no virtual callback raised when the SDK assigns it, and
no constructor overload accepts a provider. Widening the setter to `protected`
would not help either, because a decorator has to set the property on a
*different* instance of an unrelated type.

There is one supported alternative, and it was rejected on cost. The SDK does
wire every processor it owns, and `CompositeProcessor<T>` is public and forwards
`SetParentProvider` to its children — including from a subclass in another
assembly. So a component that wrapped a `BaseProcessor<T>` instead of a
`BaseExporter<T>` would have the inner exporter wired by the SDK, with no
internals access at all.

That restructuring was not taken because it trades one coupling for a larger
redesign: the component would give up the batching and flush timer it inherits
from `BatchExportProcessor` and reimplement windowing on top of `OnEnd`, and the
exporter-decorator form would have to be dropped. Crucially it would not make the
component internals-free, because retention still depends on the internal
`LogRecord.Copy()` and no public equivalent exists. The coupling is therefore
kept, but confined to one class, degraded to a no-op on failure, and covered by
a test that fails if the member can no longer be reached.

## Per-span log coverage

When `MaxLogsPerSpanPerWindow` is greater than zero, an in-span record is
offered to both:

* the whole-stream sampler; and
* an ephemeral sampler for its span, with
  `MaxLogsPerSpanPerWindow` as its budget.

The option defaults to zero, so per-span coverage is opt-in. When it is enabled
the whole-stream budget no longer bounds the number of forwarded records,
because span-only survivors are forwarded in addition to the stream sample.

This bounds the number of selected logs from a chatty span while allowing a
record rejected by the stream sampler to be retained for span-local analysis.
The exporter forwards the union of both samples.

Per-span samplers are keyed by span id and live only for the window in which
their span emits. Rather than starting from no knowledge, each is seeded with a
shared reference to the whole-stream sampler's converged weights, so a rare
callsite is favoured within the span from its first record. Sharing is safe
because the stream sampler replaces its weight table wholesale when a window
closes and never mutates a published one. Seeding affects only variance:
Horvitz-Thompson estimation stays unbiased for any positive weights, because
the inclusion probability is derived from the same weights used for admission.

Per-span records use the separate estimator:

```text
otel.span_logs.adjusted_count = 1 / per-span inclusion probability
```

As with the stream count, a value of one is omitted. A forwarded in-span record
that was not selected by its span sampler receives a per-span count of zero so
it does not inflate the per-span estimate.

Per-span coverage groups records by the span id already carried on the record,
so it works with `BottomFloorLogExporter` behind any batch processor.

This feature covers logs associated with an activity that is already
recording. It does not cause an activity to be recorded or sampled and does not
retain or export the span itself.
