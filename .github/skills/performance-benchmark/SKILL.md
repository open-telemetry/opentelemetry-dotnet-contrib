---
name: performance-benchmark
description: Generate and run ad hoc BenchmarkDotNet benchmarks to validate the performance impact of a code change in opentelemetry-dotnet-contrib. Use this when asked to benchmark, profile, or validate performance claims in a PR.
---

# Ad Hoc Performance Benchmarking

When you need to validate the performance impact of a code change, write a
BenchmarkDotNet benchmark in the affected component's `*.Benchmarks` project and
use the repository's `benchmark.ps1` script to compare a target ref against a
baseline ref. Unlike a manual build-and-swap workflow, `benchmark.ps1` handles
checking out each ref, building, and running the benchmark for you.

Per `REVIEW.md` "Performance": any PR that claims a performance improvement
should include BenchmarkDotNet results to substantiate the claim, and
measurement should happen before *and* after the change rather than relying on
assumptions.

## Step 1: Locate or Create the Benchmark Project

Benchmark projects live at `test/OpenTelemetry.{Type}.{Name}.Benchmarks/`
alongside the corresponding `test/OpenTelemetry.{Type}.{Name}.Tests/` project
(see `AGENTS.md` "Repository Layout"). Check whether one already exists for the
component you're changing before creating a new one - e.g.
`test/OpenTelemetry.Instrumentation.Http.Benchmarks/`.

If a benchmark project doesn't exist yet, base the new project on an existing
one such as `test/OpenTelemetry.Instrumentation.Http.Benchmarks/`:

- `OutputType` is `Exe` and `TargetFrameworks` is `$(SupportedNetTargets)`.
- `Program.cs` is a one-liner:
  `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)`.
- Reference `BenchmarkDotNet` via `Directory.Packages.props` (do not pin a
  version in the project file - see `AGENTS.md` "Package Management").
- Add a `<ProjectReference>` to the component under test (benchmark projects are
  the one exception to referencing the shipping assembly directly rather than
  linking Shared source).
- Follow the same folder convention as tests when there are multiple benchmark
  classes (e.g. `Instrumentation/`, `Exporter/`).

## Step 2: Write the Benchmark

### Best Practices

- **Move initialization to `[GlobalSetup]`/`[GlobalCleanup]`**: keep
  setup/teardown out of the measured method.
- **Avoid manual loops**: BenchmarkDotNet invokes the method many times
  automatically.
- **No side effects**: benchmarks must be pure and produce consistent results
  across runs.
- **Focus on the hot path being changed**: instrumentation and exporter code
  runs per-request, so benchmark the common case, not error paths.
- **Use `[MemoryDiagnoser]`** when the change affects allocations - most
  instrumentation benchmarks in this repository enable it (see `REVIEW.md`
  "Performance" - avoiding unnecessary allocations on the hot path is a stated
  goal).
- If the benchmark environment's processors include both performance and
  efficiency cores (e.g., on Apple M1/M2 or Intel hybrid architectures),
  consider pinning the benchmark to the performance cores to reduce noise.
  This can be achieved using the `--affinity` option in BenchmarkDotNet to
  specify an affinity mask to set for the benchmark process.
- **Use `[Params]`** to compare variants relevant to the change (e.g.
  instrumentation enabled vs. disabled, or old vs. new semantic-convention
  opt-in mode) in a single run rather than writing near-duplicate benchmark
  methods.
- **Benchmark class requirements**: `public`, not `sealed`, not `static`, and a
  `class` (not a `struct`).

### Example

```csharp
// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Instrumentation.Example.Benchmarks;

[MemoryDiagnoser]
public class ExampleBenchmarks
{
    private TracerProvider? tracerProvider;

    [Params(false, true)]
    public bool EnableInstrumentation { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (this.EnableInstrumentation)
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddExampleInstrumentation()
                .Build();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.tracerProvider?.Dispose();

    [Benchmark]
    public void DoWork()
    {
        // Exercise the code path being changed.
    }
}
```

Also follow the general
[BenchmarkDotNet microbenchmark design guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md)
that the .NET runtime itself follows for their own benchmarks:
no manual loops (BenchmarkDotNet iterates for you), return values to avoid
dead-code elimination, keep setup out of the measured method, and use
consistent input data across runs.

## Step 3: Run the Benchmark with `benchmark.ps1`

The repository root has a `benchmark.ps1` script that checks out a target ref
and an optional baseline ref (default `main`), builds, and runs the benchmark
for each, writing artifacts to `BenchmarkDotNet.Artifacts/<ref-name>/`. **It
requires a clean working tree** because it switches refs while running - commit
or stash changes first.

The selected benchmark must exist with the same name in both refs. For a new
benchmark, place the benchmark in a benchmark-only commit and use that commit
for `-Baseline`; comparing directly with `main` cannot produce a baseline
result for a benchmark that is not yet present in that branch. Be careful to
commit only the benchmark change(s) and not the change to the main code itself.

```powershell
# Compare the current branch against main for a specific filter
./benchmark.ps1 "Instrumentation.Http" @("*HttpClientInstrumentationBenchmarks*")

# Compare an explicit target branch against main, with memory diagnostics
./benchmark.ps1 "Instrumentation.AspNetCore" @("*SamplerBenchmarks*") -Target my-feature -EnableMemoryDiagnoser

# Only run the current branch (no baseline comparison)
./benchmark.ps1 "Contrib.Shared" @("*SQL*") -SkipBaseline

# Run against multiple TFMs, including net472 (Windows only)
./benchmark.ps1 "Exporter.Geneva" @("*") -Runtimes @("net10.0", "net472") -Job Short
```

Key parameters:

- `ProjectName` - accepts the short name (`Instrumentation.Http`) or
  fully-qualified name; the script resolves it against
  `test/OpenTelemetry*.Benchmarks/`.
- `Benchmarks` - one or more `--filter` expressions.
- `Target` / `Baseline` - refs to compare; `Baseline` defaults to `main`.
- `Job` - a BenchmarkDotNet job name (e.g. `Short`) for faster iteration.
- `Runtimes` - target frameworks; `.NET Framework` runtimes only run on Windows.
- `-EnableMemoryDiagnoser` / `-EnableEventPipeProfiler` - opt-in diagnostics.
- `-SkipBaseline` - benchmark only the target ref.

Run `Get-Help ./benchmark.ps1 -Full` for the complete parameter reference - it's
the authoritative source for script behavior and should not be duplicated here.

Alternatively, for interactive exploration of a single project without a
baseline comparison, run it directly as documented in the project's own
`README.md`, e.g.:

```sh
cd test/OpenTelemetry.Instrumentation.Http.Benchmarks
dotnet run -c Release -f net10.0 -- -m
```

See the
[BenchmarkDotNet console args guide](https://benchmarkdotnet.org/articles/guides/console-args.html)
for the raw CLI form used by both invocations.

## Step 4: Report Results

- Include the BenchmarkDotNet results table (Mean/Error/StdDev, and Allocated
  when `[MemoryDiagnoser]` is used) in the PR description or review comment -
  per `REVIEW.md` "Performance", this is required evidence for any performance
  claim, not optional supporting material.
  A paraphrased summary for a reviewer to glance at may be optionally included.
- If results show a regression in a specific scenario even though the overall
  change is a net improvement, call it out explicitly rather than omitting it -
  per the `pr-assessment.md` "Evidence & Data" guidance in the `code-review`
  skill.

> [!NOTE]
> **AI-generated content disclosure:** When posting benchmark results to
> GitHub under a user's credentials - i.e. the account is **not** a dedicated
> "copilot" or "bot" account/app - include a concise, visible disclosure note
> indicating the content was AI/Copilot-generated. Skip this only if the user
> explicitly asks you to omit it.

---

Build/test commands and general conventions are in
[`AGENTS.md`](../../../AGENTS.md); performance-specific review rules
(allocation guidance, `FrozenSet<T>` usage, `stackalloc` limits, benchmark
baseline marking) are in [`REVIEW.md`](../../../REVIEW.md). This skill only
covers how to produce the benchmark evidence those rules ask for.
