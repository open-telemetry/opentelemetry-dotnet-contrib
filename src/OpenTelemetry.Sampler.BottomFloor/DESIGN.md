# Bottom-Floor log sampler design

This document describes the design implemented by the
`OpenTelemetry.Sampler.BottomFloor` component in this change. See `README.md`
for usage.

## Scope

The component provides:

* a fixed-memory, per-window sampler for callsite-labelled events;
* an OpenTelemetry log exporter that applies the sampler to export batches; and
* a batching log processor that captures the context needed by the exporter.

The component samples logs and estimates log counts. It does not make tracing
sampling decisions, export spans, coordinate sampling across processes, or
transport sampling feedback.

## Local sampler

The internal `BottomFloorSampler<TCallsite>` is independent of OpenTelemetry
types. Its only required setting is the per-window budget `k`.

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
was fully included.

For records that were subsampled, `otel.logs.cv2` contains the squared
coefficient of variation of the callsite estimate.

Both attribute names are configurable through
`BottomFloorLogSamplerOptions`.

## Log record retention

Log records delivered by the batching pipeline may be pooled and reclaimed as
the input batch is enumerated. The exporter therefore creates a self-contained
copy when the sampler admits a record.

The copied record preserves the data needed by the inner exporter, including
attributes and instrumentation scope.

The copy is created by calling the SDK's internal `LogRecord.Copy()`. That
method is reached through `UnsafeAccessor` on `net8.0` and later, and through a
reflected delegate on `netstandard2.0` and `net462`, where that attribute does
not exist. The delegate is created once and cached, so the downlevel path costs
no more per record than the accessor does.

If it cannot be bound - for example because a future SDK renames or
removes the method - the exporter degrades to forwarding every batch unsampled
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

Unlike the record copy, this mechanism has no safe fallback. If the setter
cannot be reached, the wrapped exporter is left with the `ParentProvider` it
already had, which for a decorated exporter is none at all: the SDK only ever
assigns the property to the decorator. A resource-aware exporter such as OTLP
dereferences that provider when it resolves its `Resource`, so a failure to bind
surfaces as an error at export time rather than as a silent degradation. This is
the stronger of the two reasons to prefer a supported upstream API.

### Alternatives considered

`BaseExporter<T>` exposes no supported seam for this. Its `ParentProvider` setter
is `internal`, there is no virtual callback raised when the SDK assigns it, and
no constructor overload accepts a provider. Widening the setter to `protected`
would not help either, because a decorator has to set the property on a
*different* instance of an unrelated type.

There is one supported alternative, and it was rejected on cost. The SDK does
wire every processor it owns, and `CompositeProcessor<T>` is public and forwards
`SetParentProvider` to its children - including from a subclass in another
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
