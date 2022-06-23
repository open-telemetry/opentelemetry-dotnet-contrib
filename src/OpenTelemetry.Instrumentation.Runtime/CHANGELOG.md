# Changelog

## Unreleased

* Update a few metrics to be in sync of doc updates for `gc.heap`,
  `gc.fragmentation.ratio`, `time.in.jit`, `process.cpu.count` and `assembly.count`
  ([#430](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/430))
* Remove Process related metrics from .NET Runtime metrics
  ([#446](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/446))
* Add `exception.count` in Runtime metrics
  ([#431](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/431))

## 0.2.0-alpha.1

* Updated OTel SDK package version to 1.3.0
  ([#411](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/411))
* Fix some bugs in Runtime metrics
  ([#409](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/409))
* Add GC heap size and refactor GC count as multi-dimensional metrics in Runtime
  metrics ([#412](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/412))

## 0.1.0-alpha.1

* This is the first release of `OpenTelemetry.Instrumentation.Runtime` package.

For more details, please refer to the [README](README.md).
