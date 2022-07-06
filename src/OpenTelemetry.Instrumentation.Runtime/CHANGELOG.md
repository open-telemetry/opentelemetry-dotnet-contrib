# Changelog

## Unreleased

## 1.0.0-rc.1

Released 2022-Jun-29

Major refactor of the runtime instrumentation. Renamed API signature and metrics.
Removed the options to turn off certain metrics.

## 1.0.0-beta.1

Major redesign of the runtime instrumentation. Renamed metrics to be more user-friendly
and better logical grouping. Removed the process related metrics which are not
.NET Runtime specific.

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
