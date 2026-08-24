# Bottom-Floor sampler example

This console example runs a synthetic log workload through the Bottom-Floor log
sampler and checks that the sampler's adjusted counts recover the arrival counts
of the generating distribution.

## What it does

Each of 300 windows emits 1,000 logs whose callsites are drawn from a fixed
Zipfian distribution, so the workload is stationary and heavily skewed: a few
callsites dominate and several are rare.

The sampler keeps a budget of only 100 logs per window, so it subsamples by
roughly 10x. A statistics exporter sums `otel.logs.adjusted_count` per callsite,
reading a missing attribute as one, and compares the recovered totals to the
exact arrival counts. The total is recovered to within a fraction of a percent,
and every individual callsite to within a few percent, including the rare ones a
head-based sampler would drop entirely.

## Run

```sh
dotnet run --project examples/bottom-floor/Examples.BottomFloor.csproj
```
