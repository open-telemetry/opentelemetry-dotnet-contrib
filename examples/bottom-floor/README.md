# Bottom-Floor sampler example

This console example runs a synthetic single-process workload of correlated logs
and spans through the Bottom-Floor log sampler and checks that the sampler's
adjusted counts recover the arrival counts of the generating distribution.

## What it does

Each of 300 windows emits about 100 recording spans and 1,000 logs. Log callsites
and span operations are drawn from a fixed Zipfian distribution, so the workload
is stationary. Spans are recorded without a span exporter, so in-context logs
carry a span id and are covered whether or not spans are exported. Each in-span
log names its operation in an ordinary structured property, which the sampler
preserves on every record it keeps.

The sampler keeps only a small budget per window, 100 logs overall and 5 per span,
so it subsamples heavily. A statistics exporter sums the counts the sampler stamps
on the survivors:

* `otel.logs.adjusted_count`, summed per callsite, recovers the whole-stream
  arrival count. A missing count reads as one; a count of zero marks a span-only
  record that must not bias the stream total.
* `otel.span_logs.adjusted_count`, summed per operation, recovers the in-span
  arrival count.

The recovered totals are compared to the exact arrival counts. Both match to
within roughly one percent, and no span retains more than its per-span budget.

## Run

```sh
dotnet run --project examples/bottom-floor/Examples.BottomFloor.csproj
```
