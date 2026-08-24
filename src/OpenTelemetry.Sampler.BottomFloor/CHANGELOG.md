# Changelog - OpenTelemetry.Sampler.BottomFloor

## Unreleased

Initial release of `OpenTelemetry.Sampler.BottomFloor`.

* Added `BottomFloorLogRecordProcessor`, a log processor that caps how many
  records leave the process per window while keeping every callsite
  represented, and stamps each forwarded record with
  `otel.logs.adjusted_count` so downstream sums reconstruct pre-sampling
  counts.
  ([#5108](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5108))
* Added `BottomFloorLogExporter`, the exporter decorator the processor is built
  on, for callers who supply their own processor.
  ([#5108](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5108))
* Added `BottomFloorLogSamplerOptions` to configure the per-window `Budget` and
  the names of the stamped attributes.
  ([#5108](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/5108))

For more details, please refer to the [README](README.md).
